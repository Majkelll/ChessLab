using ChessLab.Core.Chess;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;

namespace ChessLab.Core.Tests.Draft;

public class GameStateTests
{
    private static GameState NewGame() => new(new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero));

    [Fact]
    public void NewGame_OpensWithWhitesPickAndAFullPool()
    {
        var game = NewGame();

        Assert.Equal(DraftPhase.Drafting, game.Phase);
        Assert.Equal(Side.White, game.SideToPick);
        Assert.Equal(2, game.Pool[PieceKind.Queen]);
        Assert.Equal(GameState.Budget, game.BudgetLeft(Side.White));
    }

    [Fact]
    public void Pick_FollowsSnakeOrder()
    {
        var game = NewGame();

        game.Pick(Side.White, PieceKind.Queen);
        Assert.Equal(Side.Black, game.SideToPick);

        game.Pick(Side.Black, PieceKind.Queen);
        Assert.Equal(Side.Black, game.SideToPick);

        game.Pick(Side.Black, PieceKind.Rook);
        Assert.Equal(Side.White, game.SideToPick);
    }

    [Fact]
    public void Pick_TakesThePieceOutOfTheSharedPoolAndOffTheBudget()
    {
        var game = NewGame();

        game.Pick(Side.White, PieceKind.Queen);

        Assert.Equal(1, game.Pool[PieceKind.Queen]);
        Assert.Equal(GameState.Budget - 9, game.BudgetLeft(Side.White));
        Assert.Equal([PieceKind.Queen], game.PicksOf(Side.White));
    }

    [Fact]
    public void Pick_OutOfTurn_Throws()
    {
        var game = NewGame();

        Assert.Throws<InvalidOperationException>(() => game.Pick(Side.Black, PieceKind.Queen));
    }

    [Fact]
    public void Pick_RaisesThatKindsPriceForBothSides()
    {
        var game = NewGame();

        Assert.Equal(9, game.PriceOf(PieceKind.Queen));
        Assert.Equal(5, game.PriceOf(PieceKind.Rook));

        game.Pick(Side.White, PieceKind.Queen);

        Assert.Equal(13, game.PriceOf(PieceKind.Queen));
        Assert.Equal(GameState.Budget - 9, game.BudgetLeft(Side.White));

        game.Pick(Side.Black, PieceKind.Rook);

        Assert.Equal(7, game.PriceOf(PieceKind.Rook));
        Assert.Equal(GameState.Budget - 5, game.BudgetLeft(Side.Black));
    }

    [Fact]
    public void Pick_AtTheRaisedPrice_CostsWhatItSaidWhenItWasTaken()
    {
        var game = NewGame();

        game.Pick(Side.White, PieceKind.Rook);
        game.Pick(Side.Black, PieceKind.Rook);
        game.Pick(Side.Black, PieceKind.Rook);

        Assert.Equal(GameState.Budget - 7 - 9, game.BudgetLeft(Side.Black));
    }

    [Fact]
    public void Pick_WhatTheRaisedPriceHasPutOutOfReach_IsNotOffered()
    {
        var game = NewGame();

        game.Pick(Side.White, PieceKind.Queen);
        game.Pick(Side.Black, PieceKind.Queen);
        game.Pick(Side.Black, PieceKind.Knight);
        game.Pick(Side.White, PieceKind.Rook);

        Assert.False(game.CanPick(Side.White, PieceKind.Queen));
    }

    [Fact]
    public void StartingPlay_TurnsEveryUnspentPointIntoTimeOnThatSidesClock()
    {
        var clock = new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero);
        var game = new GameState(clock);

        game.Pick(Side.White, PieceKind.Queen);
        game.Pass(Side.Black);
        game.Pass(Side.White);

        game.Place(Side.White, PieceKind.King, Square.Parse("e1"));
        game.Place(Side.White, PieceKind.Queen, Square.Parse("d1"));
        game.Place(Side.Black, PieceKind.King, Square.Parse("e8"));

        Assert.Equal(DraftPhase.Playing, game.Phase);
        Assert.Equal(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds((GameState.Budget - 9) * GameState.SecondsPerUnspentPoint),
            clock.Remaining(Side.White));
        Assert.Equal(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(GameState.Budget * GameState.SecondsPerUnspentPoint),
            clock.Remaining(Side.Black));
    }

    [Fact]
    public void Place_TheKingOnTheSecondRank_IsAllowed()
    {
        var game = NewGame();
        game.Pass(Side.White);
        game.Pass(Side.Black);

        game.Place(Side.White, PieceKind.King, Square.Parse("c2"));

        Assert.True(game.HasFinishedPlacing(Side.White));
    }

    [Fact]
    public void Place_TheKingWhereThePawnsWouldNotFit_Throws()
    {
        var game = NewGame();
        while (game.SideToPick is { } side)
        {
            if (game.PicksOf(side).Count(pick => pick == PieceKind.Pawn) < GameState.MaxPawns)
                game.Pick(side, PieceKind.Pawn);
            else
                game.Pass(side);
        }

        Assert.Throws<InvalidOperationException>(() =>
            game.Place(Side.White, PieceKind.King, Square.Parse("c2")));
    }

    [Fact]
    public void SquaresFor_OffersOnlyWhatThatPieceMayStandOn()
    {
        var game = NewGame();
        game.Pick(Side.White, PieceKind.Rook);
        game.Pass(Side.Black);
        game.Pass(Side.White);

        Assert.All(game.SquaresFor(Side.White, PieceKind.Rook), square => Assert.Equal(0, square.Rank));
        Assert.All(game.SquaresFor(Side.White, PieceKind.Pawn), square => Assert.Equal(1, square.Rank));
        Assert.Equal(16, game.SquaresFor(Side.White, PieceKind.King).Count);
    }

    [Fact]
    public void Pick_BeyondTheBudget_Throws()
    {
        var game = NewGame();
        DraftUntilDone(game, (_, _) => PieceKind.Queen);

        Assert.All(new[] { Side.White, Side.Black }, side => Assert.True(game.BudgetLeft(side) >= 0));
    }

    [Fact]
    public void Pass_TakesTheSideOutOfTheDraftForGood()
    {
        var game = NewGame();

        game.Pass(Side.White);
        game.Pick(Side.Black, PieceKind.Queen);

        Assert.Equal(Side.Black, game.SideToPick);
        Assert.True(game.HasPassed(Side.White));
    }

    [Fact]
    public void Pass_ByBothSides_MovesOnToLayingThePiecesOut()
    {
        var game = NewGame();

        game.Pass(Side.White);
        game.Pass(Side.Black);

        Assert.Equal(DraftPhase.Placing, game.Phase);
        Assert.Null(game.SideToPick);
    }

    [Fact]
    public void Place_APawnOffTheSecondRank_Throws()
    {
        var game = NewGame();
        game.Pick(Side.White, PieceKind.Pawn);
        game.Pass(Side.Black);
        game.Pass(Side.White);

        Assert.Throws<InvalidOperationException>(() =>
            game.Place(Side.White, PieceKind.Pawn, Square.Parse("a1")));
    }

    [Fact]
    public void Place_AnOfficerOnThePawnRank_Throws()
    {
        var game = NewGame();
        game.Pick(Side.White, PieceKind.Rook);
        game.Pass(Side.Black);
        game.Pass(Side.White);

        Assert.Throws<InvalidOperationException>(() =>
            game.Place(Side.White, PieceKind.Rook, Square.Parse("a2")));
    }

    [Fact]
    public void Place_OutsideTheSidesOwnTwoRanks_Throws()
    {
        var game = NewGame();
        game.Pass(Side.White);
        game.Pass(Side.Black);

        Assert.Throws<InvalidOperationException>(() =>
            game.Place(Side.White, PieceKind.King, Square.Parse("e4")));
    }

    [Fact]
    public void Place_WhenBothSidesAreDone_StartsTheGame()
    {
        var game = NewGame();
        game.Pick(Side.White, PieceKind.Rook);
        game.Pick(Side.Black, PieceKind.Rook);
        game.Pass(Side.Black);
        game.Pass(Side.White);

        game.Place(Side.White, PieceKind.King, Square.Parse("e1"));
        game.Place(Side.White, PieceKind.Rook, Square.Parse("a1"));
        game.Place(Side.Black, PieceKind.King, Square.Parse("e8"));

        Assert.Equal(DraftPhase.Placing, game.Phase);

        game.Place(Side.Black, PieceKind.Rook, Square.Parse("h8"));

        Assert.Equal(DraftPhase.Playing, game.Phase);
        Assert.Equal("4k2r/8/8/8/8/8/8/R3K3 w - - 0 1", game.Fen);
        Assert.NotEmpty(game.AvailableMoves);
    }

    [Fact]
    public void AvailableMoves_AskedForBeforeTheGameStarts_DoNotStayEmptyOnceItDoes()
    {
        var game = NewGame();
        Assert.Empty(game.AvailableMoves);

        game.Pass(Side.White);
        Assert.Empty(game.AvailableMoves);

        game.Pass(Side.Black);
        game.Place(Side.White, PieceKind.King, Square.Parse("e1"));
        Assert.Empty(game.AvailableMoves);

        game.Place(Side.Black, PieceKind.King, Square.Parse("e8"));

        Assert.NotEmpty(game.AvailableMoves);
    }

    [Fact]
    public void Unplace_PutsThePieceBackInHand()
    {
        var game = NewGame();
        game.Pass(Side.White);
        game.Pass(Side.Black);
        game.Place(Side.White, PieceKind.King, Square.Parse("e1"));

        game.Unplace(Side.White, Square.Parse("e1"));

        Assert.Equal(1, game.Remaining(Side.White, PieceKind.King));
        Assert.False(game.HasFinishedPlacing(Side.White));
    }

    [Fact]
    public void MakeMove_BeforeTheArmiesAreBuilt_Throws()
    {
        var game = NewGame();

        Assert.Throws<InvalidOperationException>(() =>
            game.MakeMove(Square.Parse("e2"), Square.Parse("e4"), null, TimeSpan.Zero));
    }

    private static void DraftUntilDone(GameState game, Func<Side, GameState, PieceKind> choose)
    {
        while (game.SideToPick is { } side)
        {
            var kind = choose(side, game);
            if (game.CanPick(side, kind))
                game.Pick(side, kind);
            else
                game.Pass(side);
        }
    }
}
