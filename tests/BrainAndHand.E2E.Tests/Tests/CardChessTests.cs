using System.Text.RegularExpressions;
using BrainAndHand.Core.CardChess;

namespace BrainAndHand.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class CardChessTests(WebAppFixture app, PlaywrightFixture playwright)
{
    [Fact]
    public async Task Lobby_for_a_card_chess_room_shows_a_two_seat_grid_and_gates_start_on_both_seats()
    {
        var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var room = await host.Home.CreateCardChessRoomAsync();

        await Expect(room.FillSeatsHint).ToContainTextAsync("both seats");
        Assert.False(await room.IsStartGameEnabledAsync());

        await room.ClaimSeatAsync(Side.White, SeatRole.Player);
        await Expect(room.FillSeatsHint).ToBeVisibleAsync();

        var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(room.Code);
        await guestRoom.ClaimSeatAsync(Side.Black, SeatRole.Player);

        await Expect(room.FillSeatsHint).ToBeHiddenAsync();
        Assert.True(await room.IsStartGameEnabledAsync());
    }

    [Fact]
    public async Task Both_sides_start_with_a_five_card_hand_and_can_play_a_full_turn_cycle()
    {
        var game = await new CardChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();

        var white = game[Side.White];
        var black = game[Side.Black];

        await Expect(white.HpWhite).ToHaveTextAsync("♥♥♥");
        await Expect(white.HpBlack).ToHaveTextAsync("♥♥♥");
        await Expect(white.ClockWhite).ToHaveTextAsync(new Regex(@"white \d{2}:\d{2}"));
        Assert.Equal(5, (await white.GetHandCardRanksAsync()).Count);

        var (whiteFrom, whiteTo) = await ComputeOpeningMoveAsync(white, isWhite: true);
        await white.MoveAsync(whiteFrom, whiteTo);
        await Expect(white.MoveHistory.Locator("li")).ToHaveCountAsync(1);
        // The played card was discarded and replaced — the hand never shrinks.
        Assert.Equal(5, (await white.GetHandCardRanksAsync()).Count);

        await black.WaitForTurnTextAsync("black");
        var (blackFrom, blackTo) = await ComputeOpeningMoveAsync(black, isWhite: false);
        await black.MoveAsync(blackFrom, blackTo);

        // Move history syncs to both seats via SignalR, and nobody's spent any HP yet (no
        // Emergency Move was needed for either player's opening card).
        await Expect(white.MoveHistory.Locator("li")).ToHaveCountAsync(2);
        await Expect(black.MoveHistory.Locator("li")).ToHaveCountAsync(2);
        await Expect(white.HpWhite).ToHaveTextAsync("♥♥♥");
        await Expect(white.HpBlack).ToHaveTextAsync("♥♥♥");
    }

    [Fact]
    public async Task Resigning_ends_the_game_in_favor_of_the_opponent()
    {
        var game = await new CardChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();

        await game[Side.White].ResignAsync();

        foreach (var side in new[] { Side.White, Side.Black })
        {
            var (reason, winner) = await game[side].WaitForGameOverAsync();
            Assert.Equal("resignation", reason);
            Assert.Equal("black", winner);
        }

        await Expect(game[Side.White].ResignButton).ToBeHiddenAsync();
        await Expect(game[Side.White].TurnStatus).ToBeHiddenAsync();
        await Expect(game[Side.White].BackToRoomLink).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Adding_and_changing_a_bots_difficulty_is_reflected_live_for_every_player_in_the_room()
    {
        var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateCardChessRoomAsync();

        var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);

        await hostRoom.AddBotAsync(Side.Black, SeatRole.Player);
        await Expect(hostRoom.SeatBotSelect(Side.Black, SeatRole.Player)).ToHaveValueAsync("Medium");
        await Expect(guestRoom.SeatBotSelect(Side.Black, SeatRole.Player)).ToHaveValueAsync("Medium");

        // The seat already has a bot, so change its difficulty via the select directly — unlike
        // SetBotAsync, which assumes an empty seat and clicks "Add bot" first.
        await hostRoom.SeatBotSelect(Side.Black, SeatRole.Player).SelectOptionAsync(nameof(BotDifficulty.Hard));
        await Expect(guestRoom.SeatBotSelect(Side.Black, SeatRole.Player)).ToHaveValueAsync("Hard");

        await hostRoom.ClearBotAsync(Side.Black, SeatRole.Player);
        await Expect(guestRoom.SeatJoinButton(Side.Black, SeatRole.Player)).ToBeVisibleAsync();

        // Filling the other seat with a human plus a bot should now be enough to start.
        await hostRoom.ClaimSeatAsync(Side.White, SeatRole.Player);
        await hostRoom.SetBotAsync(Side.Black, SeatRole.Player, BotDifficulty.Easy);
        Assert.True(await hostRoom.IsStartGameEnabledAsync());
    }

    [SkippableFact]
    public async Task Solo_human_completes_a_full_round_trip_against_a_bot_opponent()
    {
        Skip.IfNot(app.HasStockfish, "No `stockfish` binary found on PATH — install it to run bot-move tests.");

        var game = await new CardChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithBot(Side.Black, BotDifficulty.Easy)
            .StartAsync();

        var white = game[Side.White];

        Assert.Equal(0, await white.MoveHistoryCountAsync());
        var (from, to) = await ComputeOpeningMoveAsync(white, isWhite: true);
        await white.MoveAsync(from, to);

        // The bot moves on its own turn, then control returns to the human — give the engine
        // plenty of room to start up and think rather than pin this to a tight deadline.
        await Expect(white.MoveHistory.Locator("li")).ToHaveCountAsync(2, new() { Timeout = 30000 });
        await Expect(white.TurnStatus).ToContainTextAsync("white");
    }

    /// <summary>Works out the one legal opening move a hand card must resolve to. Only pawn (2–9)
    /// and knight (10) cards can ever have a legal move on a side's very first turn — every other
    /// piece is still boxed in by the starting position — and the server never deals a card with no
    /// legal move in the first place, so every dealt card is workable; the filter below is just
    /// defensive. The deck itself is shuffled server-side, so this reads whichever card actually
    /// showed up rather than trying to control it the way the Core unit tests can.</summary>
    private static async Task<(string From, string To)> ComputeOpeningMoveAsync(CardChessGamePage page, bool isWhite)
    {
        var ranks = await page.GetHandCardRanksAsync();
        var workable = ranks.First(r => r.ToPieceKind() is PieceKind.Pawn or PieceKind.Knight);
        return OpeningMoveFor(workable, isWhite);
    }

    private static (string From, string To) OpeningMoveFor(CardRank card, bool isWhite)
    {
        var homeRank = isWhite ? "2" : "7";
        var advancedRank = isWhite ? "4" : "5";
        return card switch
        {
            CardRank.Two => ($"a{homeRank}", $"a{advancedRank}"),
            CardRank.Three => ($"b{homeRank}", $"b{advancedRank}"),
            CardRank.Four => ($"c{homeRank}", $"c{advancedRank}"),
            CardRank.Five => ($"d{homeRank}", $"d{advancedRank}"),
            CardRank.Six => ($"e{homeRank}", $"e{advancedRank}"),
            CardRank.Seven => ($"f{homeRank}", $"f{advancedRank}"),
            CardRank.Eight => ($"g{homeRank}", $"g{advancedRank}"),
            CardRank.Nine => ($"h{homeRank}", $"h{advancedRank}"),
            CardRank.Ten => isWhite ? ("b1", "c3") : ("b8", "c6"),
            _ => throw new InvalidOperationException($"'{card}' can't have a legal move on the opening turn."),
        };
    }
}
