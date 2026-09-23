using ChessLab.Core.Alice;
using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Tests.Alice;

public class GameStateTests
{
    private static GameState NewGame(PieceBoard? boardA = null, PieceBoard? boardB = null) =>
        new(new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero), boardA, boardB);

    private static ChessMove Play(GameState game, string from, string to, PieceKind? promoteTo = null) =>
        game.MakeMove(Square.Parse(from), Square.Parse(to), promoteTo, TimeSpan.Zero);

    [Fact]
    public void NewGame_StartsWithEverythingOnTheFirstBoard()
    {
        var game = NewGame();

        Assert.Equal(0, game.BoardOf(Square.Parse("e2")));
        Assert.Equal("8/8/8/8/8/8/8/8 w - - 0 1", game.FenOf(1));
    }

    [Fact]
    public void MakeMove_SendsThePieceToTheOtherBoard()
    {
        var game = NewGame();

        Play(game, "e2", "e4");

        Assert.Equal(1, game.BoardOf(Square.Parse("e4")));
        Assert.Null(game.BoardOf(Square.Parse("e2")));
    }

    [Fact]
    public void AvailableMoves_ExcludeSquaresOccupiedOnTheOtherBoard()
    {
        var game = NewGame();
        Play(game, "e2", "e4");
        Play(game, "e7", "e5");

        Assert.DoesNotContain(game.AvailableMoves, move => move.To == Square.Parse("e4"));
    }

    [Fact]
    public void AvailableMoves_AreGeneratedForBothBoards()
    {
        var game = NewGame();
        Play(game, "g1", "f3");
        Play(game, "g8", "f6");

        Assert.Contains(game.AvailableMoves, move => move.From == Square.Parse("f3"));
        Assert.Contains(game.AvailableMoves, move => move.From == Square.Parse("d2"));
    }

    [Fact]
    public void AvailableMoves_NeverIncludeCastling()
    {
        var game = NewGame(PieceBoard.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1"));

        Assert.DoesNotContain(game.AvailableMoves, move =>
            move.From == Square.Parse("e1") && move.To == Square.Parse("g1"));
    }

    [Fact]
    public void AvailableMoves_LeavingTheKingAttackedOnItsOwnBoard_AreRejected()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/8/8/8/8/8/4R3/4K3 b - - 0 1"));

        Assert.DoesNotContain(game.AvailableMoves, move =>
            move.From == Square.Parse("e8") && move.To == Square.Parse("e7"));
    }

    [Fact]
    public void MakeMove_UncoveringAnAttackTheKingCannotRunFrom_IsCheckmate()
    {
        var game = NewGame(
            PieceBoard.FromFen("k7/8/8/8/N7/8/8/R3K3 w - - 0 1"),
            PieceBoard.FromFen("7R/7R/8/8/8/8/8/8 w - - 0 1"));

        Play(game, "a4", "b6");

        Assert.True(game.IsGameOver);
        Assert.Equal(new GameEndResult(GameEndReason.Checkmate, Side.White), game.EndResult);
    }

    [Fact]
    public void AvailableMoves_ExcludeStepsOntoASquareTheOtherBoardHasCovered()
    {
        var game = NewGame(
            PieceBoard.FromFen("k7/8/8/8/8/8/8/4K3 w - - 0 1"),
            PieceBoard.FromFen("8/8/8/8/8/8/7r/8 w - - 0 1"));

        Assert.DoesNotContain(game.AvailableMoves, move => move.To == Square.Parse("e2"));
        Assert.Contains(game.AvailableMoves, move => move.To == Square.Parse("d1"));
    }

    [Fact]
    public void MoveNotations_RecordWhichBoardTheMoveWasPlayedOn()
    {
        var game = NewGame();

        Play(game, "e2", "e4");
        Play(game, "e7", "e5");

        Assert.Equal(["e4(A)", "e5(A)"], game.MoveNotations);

        Play(game, "d2", "d4");

        Assert.Equal("d4(A)", game.MoveNotations[^1]);
    }

    [Fact]
    public void MakeMove_ThatIsNotAvailable_Throws()
    {
        var game = NewGame();

        Assert.Throws<InvalidOperationException>(() => Play(game, "e2", "e5"));
    }
}
