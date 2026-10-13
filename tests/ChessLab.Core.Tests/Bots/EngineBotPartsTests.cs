using ChessLab.Bots;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Bots;

public class EngineBotPartsTests
{
    private static ChessMove Move(string from, string to, PieceKind? promoteTo = null) =>
        new(Square.Parse(from), Square.Parse(to), PieceKind.Pawn, null, promoteTo, false, false, "");

    [Theory]
    [InlineData("e2", "e4", null, "e2e4")]
    [InlineData("e7", "e8", PieceKind.Queen, "e7e8q")]
    [InlineData("a7", "b8", PieceKind.Knight, "a7b8n")]
    [InlineData("h2", "h1", PieceKind.Rook, "h2h1r")]
    [InlineData("c7", "c8", PieceKind.Bishop, "c7c8b")]
    public void ToUci_WritesSquaresAndThePromotionLetter(string from, string to, PieceKind? promoteTo, string expected) =>
        Assert.Equal(expected, Move(from, to, promoteTo).ToUci());

    [Fact]
    public void ParseUciMove_ReadsSquaresAndThePromotionPiece()
    {
        Assert.Equal((Square.Parse("e1"), Square.Parse("g1"), (PieceKind?)null), UciNotation.ParseUciMove("e1g1"));
        Assert.Equal((Square.Parse("e7"), Square.Parse("e8"), (PieceKind?)PieceKind.Knight), UciNotation.ParseUciMove("e7e8n"));
    }

    [Fact]
    public void ParseUciMove_ForAnEngineReplyThatIsNoMove_Throws() =>
        Assert.ThrowsAny<Exception>(() => UciNotation.ParseUciMove("(none)"));

    [Fact]
    public async Task StartAsync_WhenTheEngineBinaryIsMissing_Throws() =>
        await Assert.ThrowsAnyAsync<Exception>(() => StockfishEngine.StartAsync("/nonexistent/stockfish-binary"));

    [Theory]
    [InlineData(BotDifficulty.Easy, 1320, 200)]
    [InlineData(BotDifficulty.Medium, 1700, 500)]
    [InlineData(BotDifficulty.Hard, 2100, 800)]
    [InlineData(BotDifficulty.Expert, 2800, 1200)]
    public void DifficultyPresets_MapEachLevelToItsRatingAndThinkingTime(BotDifficulty difficulty, int elo, int movetimeMs)
    {
        var preset = DifficultyPresets.For(difficulty);

        Assert.Equal(elo, preset.Elo);
        Assert.Equal(movetimeMs, preset.MovetimeMs);
    }

    [Fact]
    public void GameBots_NeedAnEngineOnlyForTheFourLibraryBackedModes()
    {
        GameKind[] engineKinds = [GameKind.HandAndBrain, GameKind.CardChess, GameKind.ArcaneChess, GameKind.ProgressiveChess];

        Assert.All(Enum.GetValues<GameKind>(), kind =>
            Assert.Equal(engineKinds.Contains(kind), GameBots.NeedsChessEngine(kind)));
    }
}
