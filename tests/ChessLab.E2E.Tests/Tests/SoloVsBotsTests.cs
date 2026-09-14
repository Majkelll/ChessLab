namespace ChessLab.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class SoloVsBotsTests(WebAppFixture app, PlaywrightFixture playwright)
{
    [SkippableFact]
    public async Task Solo_human_brain_completes_a_full_round_trip_against_three_bot_seats()
    {
        Skip.IfNot(app.HasStockfish, "No `stockfish` binary found on PATH — install it to run bot-move tests.");

        var game = await new GameRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, SeatRole.Brain, "Alice")
            .WithBot(Side.White, SeatRole.Hand, BotDifficulty.Easy)
            .WithBot(Side.Black, SeatRole.Brain, BotDifficulty.Easy)
            .WithBot(Side.Black, SeatRole.Hand, BotDifficulty.Easy)
            .StartAsync();

        var brain = game[Side.White, SeatRole.Brain];

        Assert.Equal(0, await brain.MoveHistoryCountAsync());
        Assert.True(await brain.IsPieceCardEnabledAsync(PieceKind.Pawn));

        await brain.SelectPieceKindAsync(PieceKind.Pawn);

        // The bot hand plays White's move, then the bot brain and bot hand play the whole of
        // Black's turn, before control returns to the human — give the engines plenty of room
        // to start up and think rather than pin this to a tight deadline.
        await Expect(brain.MoveHistory.Locator("li")).ToHaveCountAsync(2, new() { Timeout = 30000 });

        await Expect(brain.TurnStatus).ToContainTextAsync("white");
        await Expect(brain.TurnStatus).ToContainTextAsync("Brain is announcing a piece");
    }
}
