using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Progressive;

namespace ChessLab.Core.Tests.Progressive;

public class GameStateTests
{
    private static GameState NewGame(string? fen = null) =>
        new(fen is null ? new GeraChessRulesEngine() : GeraChessRulesEngine.FromFen(fen),
            new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero));

    private static void Play(GameState game, string from, string to, PieceKind? promoteTo = null) =>
        game.MakeMove(Square.Parse(from), Square.Parse(to), promoteTo, TimeSpan.Zero);

    [Fact]
    public void MakeMove_WhitesFirstSeries_IsOneMoveLong()
    {
        var game = NewGame();

        Play(game, "e2", "e4");

        Assert.Equal(Side.Black, game.SideToMove);
        Assert.Equal(2, game.SeriesNumber);
        Assert.Equal(2, game.MovesLeftInSeries);
    }

    [Fact]
    public void MakeMove_BlacksSeries_IsTwoMovesLongAndStaysWithBlack()
    {
        var game = NewGame();
        Play(game, "e2", "e4");

        Play(game, "e7", "e5");

        Assert.Equal(Side.Black, game.SideToMove);
        Assert.Equal(1, game.MovesLeftInSeries);

        Play(game, "b8", "c6");

        Assert.Equal(Side.White, game.SideToMove);
        Assert.Equal(3, game.SeriesNumber);
    }

    [Fact]
    public void MakeMove_GivingCheck_EndsTheSeriesEarly()
    {
        var game = NewGame("4k3/8/8/8/7q/8/8/4K3 w - - 0 1");
        Play(game, "e1", "d1");

        Play(game, "h4", "d4");

        Assert.Equal(Side.White, game.SideToMove);
        Assert.Equal(0, game.MovesPlayedInSeries);
        Assert.Equal(3, game.SeriesNumber);
    }

    [Fact]
    public void MakeMove_MateOnTheFirstMoveOfTheOpponentsSeries_EndsTheGame()
    {
        var game = NewGame("8/8/8/8/8/6k1/5q2/7K b - - 0 1");

        Play(game, "f2", "g2");

        Assert.True(game.IsGameOver);
        Assert.Equal(GameEndReason.Checkmate, game.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, game.EndResult!.Value.Winner);
    }

    [Fact]
    public void AvailableMoves_WhenInCheck_OnlyOfferEscapesOnTheFirstMoveOfTheSeries()
    {
        var game = NewGame("4k3/8/8/8/8/8/4q3/4K3 w - - 0 1");

        Assert.NotEmpty(game.AvailableMoves);
        Assert.All(game.AvailableMoves, move => Assert.Equal(Square.Parse("e2"), move.To));
    }

    [Fact]
    public void MoveNotations_NumberTheFirstMoveOfEverySeries()
    {
        var game = NewGame();

        Play(game, "e2", "e4");
        Play(game, "e7", "e5");
        Play(game, "b8", "c6");

        Assert.Equal(["1.e4", "2.e5", "Nc6"], game.MoveNotations);
    }

    [Fact]
    public void MakeMove_OutOfTurn_Throws()
    {
        var game = NewGame();
        Play(game, "e2", "e4");

        Assert.Throws<InvalidOperationException>(() => Play(game, "d2", "d4"));
    }
}
