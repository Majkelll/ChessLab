using ChessLab.Core.Chess;

namespace ChessLab.Core.Tests.Chess;

public class FenBoardTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public void PieceAt_ReadsPlacementCorrectly()
    {
        Assert.Equal('R', FenBoard.PieceAt(StartFen, Square.Parse("a1")));
        Assert.Equal('p', FenBoard.PieceAt(StartFen, Square.Parse("a7")));
        Assert.Null(FenBoard.PieceAt(StartFen, Square.Parse("a4")));
    }

    [Fact]
    public void MovePiece_ToEmptySquare_RelocatesPieceAndRoundTripsThroughARealEngine()
    {
        var fen = FenBoard.MovePiece(StartFen, Square.Parse("g1"), Square.Parse("g3"));

        Assert.Null(FenBoard.PieceAt(fen, Square.Parse("g1")));
        Assert.Equal('N', FenBoard.PieceAt(fen, Square.Parse("g3")));
        Assert.Equal(Side.White, GeraChessRulesEngine.FromFen(fen).SideToMove);
    }

    [Fact]
    public void MovePiece_ToNull_RemovesThePiece()
    {
        var fen = FenBoard.MovePiece(StartFen, Square.Parse("a2"), null);

        Assert.Null(FenBoard.PieceAt(fen, Square.Parse("a2")));
    }

    [Fact]
    public void MovePiece_OntoOccupiedSquare_Throws()
    {
        Assert.Throws<ArgumentException>(() => FenBoard.MovePiece(StartFen, Square.Parse("a1"), Square.Parse("a2")));
    }

    [Fact]
    public void MovePiece_FromEmptySquare_Throws()
    {
        Assert.Throws<ArgumentException>(() => FenBoard.MovePiece(StartFen, Square.Parse("a4"), Square.Parse("a5")));
    }

    [Fact]
    public void SwapPieces_ExchangesBothSquares()
    {
        var fen = FenBoard.SwapPieces(StartFen, Square.Parse("a1"), Square.Parse("e1"));

        Assert.Equal('K', FenBoard.PieceAt(fen, Square.Parse("a1")));
        Assert.Equal('R', FenBoard.PieceAt(fen, Square.Parse("e1")));
    }

    [Fact]
    public void SwapPieces_EitherSquareEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => FenBoard.SwapPieces(StartFen, Square.Parse("a1"), Square.Parse("a4")));
    }

    [Fact]
    public void MovePiece_ClearsAStaleEnPassantTarget()
    {
        const string fenWithEp = "rnbqkbnr/ppp1pppp/8/3pP3/8/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 3";

        var fen = FenBoard.MovePiece(fenWithEp, Square.Parse("b1"), Square.Parse("c3"));

        Assert.Equal("-", fen.Split(' ')[3]);
    }

    [Theory]
    [InlineData("e1", "KQ")]
    [InlineData("a1", "Q")]
    [InlineData("h1", "K")]
    [InlineData("e8", "kq")]
    [InlineData("a8", "q")]
    [InlineData("h8", "k")]
    public void MovePiece_TouchingAHomeSquare_RevokesTheMatchingCastlingRights(string square, string revoked)
    {
        var fen = FenBoard.MovePiece(StartFen, Square.Parse(square), null);
        var castling = fen.Split(' ')[2];

        foreach (var c in revoked)
            Assert.DoesNotContain(c, castling);
    }

    [Fact]
    public void MovePiece_NotTouchingAnyHomeSquare_KeepsCastlingRights()
    {
        var fen = FenBoard.MovePiece(StartFen, Square.Parse("b1"), Square.Parse("c3"));

        Assert.Equal("KQkq", fen.Split(' ')[2]);
    }

    [Fact]
    public void WithSideToMove_FlipsTurnAndClearsEnPassant()
    {
        const string fenWithEp = "rnbqkbnr/ppp1pppp/8/3pP3/8/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 3";

        var fen = FenBoard.WithSideToMove(fenWithEp, Side.Black);
        var fields = fen.Split(' ');

        Assert.Equal("b", fields[1]);
        Assert.Equal("-", fields[3]);
        Assert.Equal(fenWithEp.Split(' ')[0], fields[0]);
    }
}
