using System.Text.RegularExpressions;

namespace BrainAndHand.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class GameplayTests(WebAppFixture app, PlaywrightFixture playwright)
{
    private Task<StartedGame> StartFourHumanGameAsync() =>
        new GameRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, SeatRole.Brain, "Alice")
            .WithHuman(Side.White, SeatRole.Hand, "Bob")
            .WithHuman(Side.Black, SeatRole.Brain, "Carol")
            .WithHuman(Side.Black, SeatRole.Hand, "Dave")
            .StartAsync();

    [Fact]
    public async Task Only_the_seat_whose_turn_it_is_can_act_and_the_turn_alternates_correctly()
    {
        var game = await StartFourHumanGameAsync();
        var whiteBrain = game[Side.White, SeatRole.Brain];
        var whiteHand = game[Side.White, SeatRole.Hand];
        var blackBrain = game[Side.Black, SeatRole.Brain];

        // Game just started: it's White's turn to announce a piece. Only a handful of piece
        // kinds have a legal opening move, and only White Brain's cards should be clickable.
        await Expect(whiteBrain.TurnStatus).ToContainTextAsync("white");
        await Expect(whiteBrain.TurnStatus).ToContainTextAsync("Brain is announcing a piece");
        Assert.True(await whiteBrain.IsPieceCardEnabledAsync(PieceKind.Pawn));
        Assert.False(await whiteBrain.IsPieceCardEnabledAsync(PieceKind.Queen));

        // Black Brain sees the same six cards, but it isn't their turn, so all of them are inert
        // regardless of which kinds would otherwise have a legal move.
        Assert.False(await blackBrain.IsPieceCardEnabledAsync(PieceKind.Pawn));

        await whiteBrain.SelectPieceKindAsync(PieceKind.Pawn);
        await whiteHand.WaitForTurnTextAsync("Hand is making a move");

        // Attempting to move a piece other than the announced kind (a knight, here) is simply a
        // no-op client-side: the board's legal-move list from the server only contains pawn moves.
        await whiteHand.MoveAsync("g1", "f3");
        Assert.Contains("Hand is making a move", await whiteHand.GetTurnStatusAsync());
        Assert.Equal(0, await whiteHand.MoveHistoryCountAsync());

        await whiteHand.MoveAsync("e2", "e4");
        await Expect(whiteHand.MoveHistory.Locator("li")).ToHaveCountAsync(1);

        // Turn passed to Black — White's own seats go quiet, Black Brain lights up.
        await blackBrain.WaitForTurnTextAsync("Brain is announcing a piece");
        await Expect(blackBrain.TurnStatus).ToContainTextAsync("black");
        Assert.False(await whiteBrain.IsPieceCardEnabledAsync(PieceKind.Pawn));
        Assert.True(await blackBrain.IsPieceCardEnabledAsync(PieceKind.Knight));

        await blackBrain.SelectPieceKindAsync(PieceKind.Knight);
        var blackHand = game[Side.Black, SeatRole.Hand];
        await blackHand.WaitForTurnTextAsync("Hand is making a move");
        await blackHand.MoveAsync("b8", "c6");

        await Expect(whiteHand.MoveHistory.Locator("li")).ToHaveCountAsync(2);
        await whiteBrain.WaitForTurnTextAsync("Brain is announcing a piece");
    }

    [Fact]
    public async Task Move_history_lists_every_completed_move_in_order_for_every_seat()
    {
        var game = await StartFourHumanGameAsync();

        await ChessScripts.PlayTurnAsync(game, Side.White, PieceKind.Pawn, "e2", "e4");
        await ChessScripts.PlayTurnAsync(game, Side.Black, PieceKind.Pawn, "e7", "e5");

        foreach (var seatGame in game.Games.Values)
            await Expect(seatGame.MoveHistory.Locator("li")).ToHaveCountAsync(2);

        await Expect(game[Side.White, SeatRole.Brain].MoveHistory).ToContainTextAsync(new Regex("1\\..*e4"));
        await Expect(game[Side.White, SeatRole.Brain].MoveHistory).ToContainTextAsync(new Regex("2\\..*e5"));
    }

    [Fact]
    public async Task Clocks_are_shown_for_both_sides_in_mm_ss_format()
    {
        var game = await StartFourHumanGameAsync();
        var whiteBrain = game[Side.White, SeatRole.Brain];

        await Expect(whiteBrain.ClockWhite).ToHaveTextAsync(new Regex(@"white \d{2}:\d{2}"));
        await Expect(whiteBrain.ClockBlack).ToHaveTextAsync(new Regex(@"black \d{2}:\d{2}"));
    }

    [Fact]
    public async Task A_clock_configured_by_the_host_in_the_room_carries_over_to_the_started_game()
    {
        var game = await new GameRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, SeatRole.Brain, "Alice")
            .WithHuman(Side.White, SeatRole.Hand, "Bob")
            .WithHuman(Side.Black, SeatRole.Brain, "Carol")
            .WithHuman(Side.Black, SeatRole.Hand, "Dave")
            .WithClock(minutes: 3, incrementSeconds: 2)
            .StartAsync();

        // Black's clock only starts ticking once it's Black's turn, so it stays exactly 03:00 —
        // White's has been visibly counting down since the game started, so allow a little drift
        // for however long setup + the assertion's own retries took.
        var whiteBrain = game[Side.White, SeatRole.Brain];
        await Expect(whiteBrain.ClockWhite).ToHaveTextAsync(new Regex(@"white 02:5\d|white 03:00"));
        await Expect(whiteBrain.ClockBlack).ToHaveTextAsync("black 03:00");
    }

    [Fact]
    public async Task Hand_can_see_which_piece_kind_the_brain_announced()
    {
        var game = await StartFourHumanGameAsync();
        var whiteBrain = game[Side.White, SeatRole.Brain];
        var whiteHand = game[Side.White, SeatRole.Hand];

        // Before an announcement, the Hand has nothing to show yet.
        await Expect(whiteHand.PieceKindCards).ToBeHiddenAsync();

        await whiteBrain.SelectPieceKindAsync(PieceKind.Knight);
        await whiteHand.WaitForTurnTextAsync("Hand is making a move");

        // The Hand sees the announced kind highlighted, not just an ambiguous "make a move".
        await Expect(whiteHand.PieceKindCards).ToBeVisibleAsync();
        Assert.True(await whiteHand.IsPieceCardAnnouncedAsync(PieceKind.Knight));
        Assert.False(await whiteHand.IsPieceCardAnnouncedAsync(PieceKind.Pawn));
        await Expect(whiteHand.TurnStatus).ToContainTextAsync("Hand is making a move (Knight)");

        // Brain sees the exact same highlight on their own (now non-interactive) card grid.
        Assert.True(await whiteBrain.IsPieceCardAnnouncedAsync(PieceKind.Knight));
        Assert.False(await whiteBrain.IsPieceCardEnabledAsync(PieceKind.Knight));
    }

    [Fact]
    public async Task Each_team_sees_a_read_only_peek_at_the_opposing_teams_current_pick()
    {
        var game = await StartFourHumanGameAsync();
        var whiteBrain = game[Side.White, SeatRole.Brain];
        var blackBrain = game[Side.Black, SeatRole.Brain];
        var blackHand = game[Side.Black, SeatRole.Hand];

        // Before White has announced anything, Black hasn't got a pick to peek at yet.
        await Expect(blackBrain.OpponentPick).ToBeHiddenAsync();

        await whiteBrain.SelectPieceKindAsync(PieceKind.Pawn);
        var whiteHand = game[Side.White, SeatRole.Hand];
        await whiteHand.WaitForTurnTextAsync("Hand is making a move");

        // Both Black seats see a read-only badge naming what White's Hand is about to move with —
        // it lives next to White's clock on Black's screen, not mixed into Black's own card grid.
        await Expect(blackBrain.OpponentPick).ToHaveTextAsync(new Regex("opponent:.*Pawn"));
        await Expect(blackHand.OpponentPick).ToHaveTextAsync(new Regex("opponent:.*Pawn"));

        // White's own seats don't get an "opponent" badge for their own team's pick.
        await Expect(whiteBrain.OpponentPick).ToBeHiddenAsync();
    }

    [Fact]
    public async Task Reaching_the_back_rank_shows_a_promotion_dialog_and_promotes_to_the_chosen_piece()
    {
        var game = await StartFourHumanGameAsync();

        // A short cooperative line — Black's replies are irrelevant fillers — that walks White's
        // b-pawn, capturing twice along the way, onto a8. That's the one situation with more than
        // one legal move sharing the same from/to (one per promotion piece), so the board has to
        // show a promotion dialog instead of just completing the move on the second click.
        await ChessScripts.PlayTurnAsync(game, Side.White, PieceKind.Pawn, "e2", "e4");
        await ChessScripts.PlayTurnAsync(game, Side.Black, PieceKind.Pawn, "e7", "e5");
        await ChessScripts.PlayTurnAsync(game, Side.White, PieceKind.Pawn, "b2", "b4");
        await ChessScripts.PlayTurnAsync(game, Side.Black, PieceKind.Pawn, "a7", "a5");
        await ChessScripts.PlayTurnAsync(game, Side.White, PieceKind.Pawn, "b4", "a5");
        await ChessScripts.PlayTurnAsync(game, Side.Black, PieceKind.Knight, "g8", "f6");
        await ChessScripts.PlayTurnAsync(game, Side.White, PieceKind.Pawn, "a5", "a6");
        await ChessScripts.PlayTurnAsync(game, Side.Black, PieceKind.Knight, "f6", "g8");
        await ChessScripts.PlayTurnAsync(game, Side.White, PieceKind.Pawn, "a6", "b7");
        await ChessScripts.PlayTurnAsync(game, Side.Black, PieceKind.Knight, "g8", "f6");

        var whiteBrain = game[Side.White, SeatRole.Brain];
        var whiteHand = game[Side.White, SeatRole.Hand];
        await whiteBrain.WaitForTurnTextAsync("Brain is announcing a piece");
        await whiteBrain.SelectPieceKindAsync(PieceKind.Pawn);
        await whiteHand.WaitForTurnTextAsync("Hand is making a move");

        await Expect(whiteHand.PromotionPicker).ToBeHiddenAsync();
        await whiteHand.MoveAsync("b7", "a8"); // captures Black's still-unmoved rook
        await Expect(whiteHand.PromotionPicker).ToBeVisibleAsync();
        // The move isn't finished yet — nobody else's turn should start until a piece is chosen.
        Assert.Equal(10, await whiteHand.MoveHistoryCountAsync());

        await whiteHand.PromoteToAsync(PieceKind.Queen);

        await Expect(whiteHand.PromotionPicker).ToBeHiddenAsync();
        await Expect(whiteHand.MoveHistory.Locator("li").Last).ToContainTextAsync(new Regex("=Q"));
        await game[Side.Black, SeatRole.Brain].WaitForTurnTextAsync("Brain is announcing a piece");
    }

    [Fact]
    public async Task Drag_and_drop_is_a_working_alternate_way_to_make_a_move()
    {
        var game = await StartFourHumanGameAsync();
        var whiteBrain = game[Side.White, SeatRole.Brain];
        var whiteHand = game[Side.White, SeatRole.Hand];

        await whiteBrain.SelectPieceKindAsync(PieceKind.Pawn);
        await whiteHand.WaitForTurnTextAsync("Hand is making a move");

        await whiteHand.DragMoveAsync("e2", "e4");

        await Expect(whiteHand.MoveHistory.Locator("li")).ToHaveCountAsync(1);
        await game[Side.Black, SeatRole.Brain].WaitForTurnTextAsync("Brain is announcing a piece");
    }
}
