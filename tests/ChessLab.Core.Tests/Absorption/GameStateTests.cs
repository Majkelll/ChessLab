using ChessLab.Core.Absorption;
using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Tests.Absorption;

public class GameStateTests
{
    private static GameState NewGame(PieceBoard? board = null) =>
        new(new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero), board);

    private static ChessMove Play(GameState game, string from, string to, PieceKind? promoteTo = null) =>
        game.MakeMove(Square.Parse(from), Square.Parse(to), promoteTo, TimeSpan.Zero);

    [Fact]
    public void MakeMove_Capturing_GivesTheCapturingPieceTheCapturedPiecesMoves()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/8/8/8/8/1r6/8/2N1K3 w - - 0 1"));

        Play(game, "c1", "b3");

        Assert.Equal(MovePower.Knight | MovePower.Rook, game.PowersAt(Square.Parse("b3")));
    }

    [Fact]
    public void AvailableMoves_ForAnAbsorbedPiece_IncludeBothSetsOfMoves()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/8/8/8/8/1r6/8/2N1K3 w - - 0 1"));
        Play(game, "c1", "b3");
        Play(game, "e8", "d8");

        var fromB3 = game.AvailableMoves.Where(move => move.From == Square.Parse("b3")).ToArray();

        Assert.Contains(fromB3, move => move.To == Square.Parse("b8"));
        Assert.Contains(fromB3, move => move.To == Square.Parse("a5"));
    }

    [Fact]
    public void PowersText_ListsOnlyPiecesCarryingMoreThanTheirOwnMoves()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/8/8/8/8/1r6/8/2N1K3 w - - 0 1"));

        Assert.Equal("", game.PowersText);

        Play(game, "c1", "b3");

        Assert.Equal("b3:N", game.PowersText);
    }

    [Fact]
    public void MakeMove_TheKingCapturing_KeepsItRoyalAndAbsorbsAsWell()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/8/8/8/8/8/4r3/4K3 w - - 0 1"));

        Play(game, "e1", "e2");

        Assert.Equal(MovePower.King | MovePower.Rook, game.PowersAt(Square.Parse("e2")));
        Assert.False(game.IsGameOver);
    }

    [Fact]
    public void MakeMove_Promoting_TradesThePawnStepForTheChosenPiece()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/P7/8/8/8/8/8/4K3 w - - 0 1"));

        Play(game, "a7", "a8", PieceKind.Queen);

        Assert.Equal(MovePower.Queen, game.PowersAt(Square.Parse("a8")));
    }

    [Fact]
    public void MakeMove_LeavingTheOpponentMatedInCheck_EndsTheGame()
    {
        var game = NewGame(PieceBoard.FromFen("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1"));

        Play(game, "a1", "a8");

        Assert.Equal(new GameEndResult(GameEndReason.Checkmate, Side.White), game.EndResult);
    }

    [Fact]
    public void MakeMove_AfterTheGameEnded_Throws()
    {
        var game = NewGame(PieceBoard.FromFen("6k1/5ppp/8/8/8/8/8/R3K3 w - - 0 1"));
        Play(game, "a1", "a8");

        Assert.Throws<InvalidOperationException>(() => Play(game, "e1", "e2"));
    }
}
