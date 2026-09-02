using System.Text.RegularExpressions;
using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.PageObjects;

public sealed class ArcaneChessGamePage(IPage page)
{
    public IPage Page { get; } = page;

    public ILocator ClockWhite => Page.GetByTestId("clock-white");
    public ILocator ClockBlack => Page.GetByTestId("clock-black");
    public ILocator HpWhite => Page.GetByTestId("hp-white");
    public ILocator HpBlack => Page.GetByTestId("hp-black");
    public ILocator TurnStatus => Page.GetByTestId("turn-status");
    public ILocator MyHand => Page.GetByTestId("my-hand");
    public ILocator MySpellHand => Page.GetByTestId("my-spell-hand");
    public ILocator ActiveEffects => Page.GetByTestId("active-effects");
    public ILocator GameOverBanner => Page.GetByTestId("game-over-banner");
    public ILocator GameOverReason => Page.GetByTestId("game-over-reason");
    public ILocator GameOverWinner => Page.GetByTestId("game-over-winner");
    public ILocator BackToRoomLink => Page.GetByTestId("back-to-room-link");
    public ILocator ResignButton => Page.GetByTestId("resign-btn");
    public ILocator MoveHistory => Page.GetByTestId("move-history");
    public ILocator ErrorMessage => Page.GetByTestId("game-error");

    public ILocator ChessBoardRoot => Page.GetByTestId("chess-board");
    public ILocator Square(string square) => ChessBoardRoot.Locator($"rect.square[data-square='{square}']");

    public ILocator SpellCard(SpellRank spell) => MySpellHand.GetByTestId($"spell-card-{spell}");
    public ILocator SpellTargetForm => Page.GetByTestId("spell-target-form");
    public ILocator SpellTargetSquare => Page.GetByTestId("spell-target-square");
    public ILocator SpellTargetSquare1 => Page.GetByTestId("spell-target-square-1");
    public ILocator SpellTargetSquare2 => Page.GetByTestId("spell-target-square-2");
    public ILocator CastSpellButton => Page.GetByTestId("cast-spell-btn");

    public async Task<IReadOnlyList<CardRank>> GetHandCardRanksAsync()
    {
        var testIds = await MyHand.Locator("[data-testid^='hand-card-']")
            .EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-testid'))");
        return testIds.Select(id => Enum.Parse<CardRank>(id["hand-card-".Length..])).ToArray();
    }

    public async Task<IReadOnlyList<SpellRank>> GetSpellHandRanksAsync()
    {
        var testIds = await MySpellHand.Locator("[data-testid^='spell-card-']")
            .EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-testid'))");
        return testIds.Select(id => Enum.Parse<SpellRank>(id["spell-card-".Length..])).ToArray();
    }

    public async Task MoveAsync(string from, string to)
    {
        await Square(from).ClickAsync();
        await Page.WaitForTimeoutAsync(100);
        await Square(to).ClickAsync();
    }

    /// <summary>Casts a spell with no target (e.g. Peek, Mend, Jam, Feint, Reshuffle, ExtraTurn).</summary>
    public async Task CastNoTargetSpellAsync(SpellRank spell)
    {
        await SpellCard(spell).ClickAsync();
        await SpellTargetForm.WaitForAsync();
        await CastSpellButton.ClickAsync();
    }

    /// <summary>Casts a spell that needs one square (e.g. Shield, Freeze Square, Pin Down, Disarm, Dispel, Execution, Mind Swap).</summary>
    public async Task CastOneSquareSpellAsync(SpellRank spell, string square)
    {
        await SpellCard(spell).ClickAsync();
        await SpellTargetSquare.WaitForAsync();
        await SpellTargetSquare.FillAsync(square);
        await CastSpellButton.ClickAsync();
    }

    /// <summary>Casts a spell that needs two squares (Swap, Teleport, Time Freeze).</summary>
    public async Task CastTwoSquareSpellAsync(SpellRank spell, string firstSquare, string secondSquare)
    {
        await SpellCard(spell).ClickAsync();
        await SpellTargetSquare1.WaitForAsync();
        await SpellTargetSquare1.FillAsync(firstSquare);
        await SpellTargetSquare2.FillAsync(secondSquare);
        await CastSpellButton.ClickAsync();
    }

    public Task ResignAsync() => ResignButton.ClickAsync();

    public Task WaitForTurnTextAsync(string containingText, int timeoutMs = 10000) =>
        TurnStatus.Filter(new LocatorFilterOptions { HasText = containingText }).WaitForAsync(new() { Timeout = timeoutMs });

    public async Task<(string Reason, string? Winner)> WaitForGameOverAsync(int timeoutMs = 15000)
    {
        await GameOverBanner.WaitForAsync(new() { Timeout = timeoutMs });
        var reason = await GameOverReason.InnerTextAsync();
        var winner = await GameOverWinner.CountAsync() > 0 ? await GameOverWinner.InnerTextAsync() : null;
        return (reason, winner);
    }

    public async Task<RoomPage> BackToRoomAsync()
    {
        await BackToRoomLink.ClickAsync();
        await Page.WaitForURLAsync(new Regex("/room/"), new() { Timeout = 15000 });
        return new RoomPage(Page);
    }
}
