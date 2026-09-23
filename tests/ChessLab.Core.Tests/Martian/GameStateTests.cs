using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Martian;

namespace ChessLab.Core.Tests.Martian;

public class GameStateTests
{
    private static GameState NewGame() => new(new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero));

    private static ChessMove Play(GameState game, string from, string to) =>
        game.MakeMove(Square.Parse(from), Square.Parse(to), null, TimeSpan.Zero);

    [Fact]
    public void NewGame_DealsNinePyramidsIntoEachHalf()
    {
        var game = NewGame();

        var white = Squares().Where(square => GameState.HalfOf(square) == Side.White && game.PyramidAt(square) != 0);
        var black = Squares().Where(square => GameState.HalfOf(square) == Side.Black && game.PyramidAt(square) != 0);

        Assert.Equal(9, white.Count());
        Assert.Equal(9, black.Count());
        Assert.Equal(3, game.PyramidAt(Square.Parse("a1")));
        Assert.Equal(1, game.PyramidAt(Square.Parse("c2")));
        Assert.Equal(3, game.PyramidAt(Square.Parse("d8")));
    }

    [Fact]
    public void AvailableMoves_OnlyEverStartFromTheSidesOwnHalf()
    {
        var game = NewGame();

        Assert.All(game.AvailableMoves, move => Assert.Equal(Side.White, GameState.HalfOf(move.From)));
    }

    [Fact]
    public void AvailableMoves_NeverLandOnAPieceInTheSidesOwnHalf()
    {
        var game = NewGame();

        Assert.All(game.AvailableMoves, move =>
            Assert.True(game.PyramidAt(move.To) == 0 || GameState.HalfOf(move.To) == Side.Black));
    }

    [Fact]
    public void AvailableMoves_ForAPawn_AreSingleDiagonalSteps()
    {
        var game = NewGame();

        var fromC2 = game.AvailableMoves.Where(move => move.From == Square.Parse("c2")).Select(move => move.To);

        Assert.Equal(["d3", "d1"], fromC2.Select(square => square.ToString()));
    }

    [Fact]
    public void AvailableMoves_ForADrone_ReachOneOrTwoSquaresStraight()
    {
        var game = NewGame();

        var fromA3 = game.AvailableMoves.Where(move => move.From == Square.Parse("a3")).Select(move => move.To.ToString());

        Assert.Contains("a4", fromA3);
        Assert.Contains("a5", fromA3);
        Assert.DoesNotContain("a6", fromA3);
    }

    [Fact]
    public void MakeMove_CrossingIntoTheOtherHalf_HandsThePieceOver()
    {
        var game = NewGame();

        Play(game, "a3", "a5");

        Assert.Contains(game.AvailableMoves, move => move.From == Square.Parse("a5"));
    }

    [Fact]
    public void MakeMove_TakingAPieceInTheOpponentsHalf_ScoresItsValue()
    {
        var game = NewGame();
        Play(game, "a3", "a5");
        Play(game, "d6", "d5");

        Play(game, "a2", "a5");

        Assert.Equal(2, game.ScoreOf(Side.White));
    }

    [Fact]
    public void AvailableMoves_ExcludePushingTheOpponentsLastMoveStraightBack()
    {
        var game = NewGame();
        Play(game, "a3", "a5");

        Assert.DoesNotContain(game.AvailableMoves, move =>
            move.From == Square.Parse("a5") && move.To == Square.Parse("a3"));
    }

    [Fact]
    public void MakeMove_WithAPromotion_Throws()
    {
        var game = NewGame();

        Assert.Throws<InvalidOperationException>(() =>
            game.MakeMove(Square.Parse("c2"), Square.Parse("d3"), PieceKind.Queen, TimeSpan.Zero));
    }

    [Fact]
    public void Resign_HandsTheGameToTheOpponent()
    {
        var game = NewGame();

        game.Resign(Side.Black);

        Assert.Equal(new GameEndResult(GameEndReason.Resignation, Side.White), game.EndResult);
    }

    private static IEnumerable<Square> Squares()
    {
        for (var file = 0; file < GameState.Files; file++)
        {
            for (var rank = 0; rank < GameState.Ranks; rank++)
                yield return new Square(file, rank);
        }
    }
}
