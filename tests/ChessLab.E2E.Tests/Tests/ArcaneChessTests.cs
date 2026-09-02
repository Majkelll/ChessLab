using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;

namespace ChessLab.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class ArcaneChessTests(WebAppFixture app, PlaywrightFixture playwright)
{
    [Fact]
    public async Task Lobby_for_an_arcane_chess_room_shows_a_two_seat_grid_and_gates_start_on_both_seats()
    {
        var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var room = await host.Home.CreateArcaneChessRoomAsync();

        await Expect(room.FillSeatsHint).ToContainTextAsync("both seats");
        Assert.False(await room.IsStartGameEnabledAsync());

        await room.ClaimSeatAsync(Side.White, SeatRole.Player);

        var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(room.Code);
        await guestRoom.ClaimSeatAsync(Side.Black, SeatRole.Player);

        await Expect(room.FillSeatsHint).ToBeHiddenAsync();
        Assert.True(await room.IsStartGameEnabledAsync());
    }

    [Fact]
    public async Task Both_sides_start_with_a_rank_hand_a_spell_hand_and_starting_mana_and_can_play_a_full_turn_cycle()
    {
        var game = await new ArcaneChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();

        var white = game[Side.White];
        var black = game[Side.Black];

        await Expect(white.HpWhite).ToHaveTextAsync("♥♥♥");
        Assert.Equal(5, (await white.GetHandCardRanksAsync()).Count);
        Assert.Equal(3, (await white.GetSpellHandRanksAsync()).Count);
        Assert.Equal(3, (await black.GetSpellHandRanksAsync()).Count);

        var (whiteFrom, whiteTo) = await ComputeOpeningMoveAsync(white, isWhite: true);
        await white.MoveAsync(whiteFrom, whiteTo);
        await Expect(white.MoveHistory.Locator("li")).ToHaveCountAsync(1);

        await black.WaitForTurnTextAsync("black");
        var (blackFrom, blackTo) = await ComputeOpeningMoveAsync(black, isWhite: false);
        await black.MoveAsync(blackFrom, blackTo);

        await Expect(white.MoveHistory.Locator("li")).ToHaveCountAsync(2);
        await white.WaitForTurnTextAsync("white");
    }

    // Spell hands are dealt 3-of-19 at random, so a test that needs a specific spell to show up
    // skips rather than flakes when this particular game didn't deal it — the other tests already
    // cover the general "a room full of Arcane Chess plays" path regardless of which spells appear.
    [SkippableFact]
    public async Task Casting_a_no_target_spell_spends_mana_and_redraws_the_card()
    {
        var game = await new ArcaneChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();

        var white = game[Side.White];
        var handBefore = await white.GetSpellHandRanksAsync();
        Skip.IfNot(handBefore.Any(s => s is SpellRank.Peek or SpellRank.Jam or SpellRank.Feint),
            "None of Peek/Jam/Feint were dealt to White this game.");
        var castable = handBefore.First(s => s is SpellRank.Peek or SpellRank.Jam or SpellRank.Feint);

        await white.CastNoTargetSpellAsync(castable);

        await Expect(white.SpellCard(castable)).ToHaveCountAsync(0);
        Assert.Equal(3, (await white.GetSpellHandRanksAsync()).Count);

        // The client disables every spell card the moment one has been cast this turn — the
        // server-side "one spell per turn" rule itself is covered by the Core unit tests.
        foreach (var spell in await white.GetSpellHandRanksAsync())
            await Expect(white.SpellCard(spell)).ToBeDisabledAsync();
    }

    [SkippableFact]
    public async Task Casting_freeze_square_shows_an_active_effect_until_the_opponents_turn_resolves()
    {
        var game = await new ArcaneChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();

        var white = game[Side.White];
        var black = game[Side.Black];

        var handBefore = await white.GetSpellHandRanksAsync();
        Skip.IfNot(handBefore.Contains(SpellRank.FreezeSquare), "Freeze Square wasn't dealt to White this game.");

        await white.CastOneSquareSpellAsync(SpellRank.FreezeSquare, "d4");
        await Expect(white.ActiveEffects).ToContainTextAsync("d4");

        var (whiteFrom, whiteTo) = await ComputeOpeningMoveAsync(white, isWhite: true);
        await white.MoveAsync(whiteFrom, whiteTo);

        await black.WaitForTurnTextAsync("black");
        await Expect(black.ActiveEffects).ToContainTextAsync("d4");
    }

    private static async Task<(string From, string To)> ComputeOpeningMoveAsync(PageObjects.ArcaneChessGamePage page, bool isWhite)
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
