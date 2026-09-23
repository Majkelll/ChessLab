using ChessLab.Core.Bidding;
using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Tests.Bidding;

public class GameStateTests
{
    private static GameState NewGame(PieceBoard? board = null) =>
        new(new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero), board);

    private static void Bid(GameState game, int white, int black)
    {
        game.SubmitBid(Side.White, white, TimeSpan.Zero);
        game.SubmitBid(Side.Black, black, TimeSpan.Zero);
    }

    [Fact]
    public void SubmitBid_HigherBidder_WinsTheMoveAndPaysTheOpponent()
    {
        var game = NewGame();

        Bid(game, 30, 10);

        Assert.Equal(BiddingPhase.Moving, game.Phase);
        Assert.Equal(Side.White, game.SideToMove);
        Assert.Equal(GameState.StartingChips - 30, game.ChipsOf(Side.White));
        Assert.Equal(GameState.StartingChips + 30, game.ChipsOf(Side.Black));
    }

    [Fact]
    public void SubmitBid_TiedBids_GoToTheMarkerHolderAndHandTheMarkerOver()
    {
        var game = NewGame();

        Bid(game, 20, 20);

        Assert.Equal(Side.White, game.SideToMove);
        Assert.Equal(Side.Black, game.MarkerHolder);
        Assert.Equal(GameState.StartingChips - 20, game.ChipsOf(Side.White));
    }

    [Fact]
    public void SubmitBid_Twice_Throws()
    {
        var game = NewGame();
        game.SubmitBid(Side.White, 5, TimeSpan.Zero);

        Assert.Throws<InvalidOperationException>(() => game.SubmitBid(Side.White, 7, TimeSpan.Zero));
    }

    [Fact]
    public void SubmitBid_MoreChipsThanTheSideHolds_Throws()
    {
        var game = NewGame();

        Assert.Throws<InvalidOperationException>(() =>
            game.SubmitBid(Side.White, GameState.StartingChips + 1, TimeSpan.Zero));
    }

    [Fact]
    public void SubmitBid_WhileAWonMoveIsStillPending_Throws()
    {
        var game = NewGame();
        Bid(game, 10, 5);

        Assert.Throws<InvalidOperationException>(() => game.SubmitBid(Side.White, 1, TimeSpan.Zero));
    }

    [Fact]
    public void MakeMove_BeforeBothSidesHaveBid_Throws()
    {
        var game = NewGame();

        Assert.Throws<InvalidOperationException>(() =>
            game.MakeMove(Square.Parse("e2"), Square.Parse("e4"), null, TimeSpan.Zero));
    }

    [Fact]
    public void MakeMove_WinningTheBidTwiceInARow_LetsTheSameSideMoveAgain()
    {
        var game = NewGame();

        Bid(game, 10, 0);
        game.MakeMove(Square.Parse("e2"), Square.Parse("e4"), null, TimeSpan.Zero);
        Bid(game, 10, 0);

        Assert.Equal(Side.White, game.SideToMove);
        Assert.Contains(game.AvailableMoves, move => move.From == Square.Parse("d2"));
    }

    [Fact]
    public void AvailableMoves_LeavingTheOwnKingAttacked_AreStillOffered()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/8/8/8/8/8/4R3/5K2 b - - 0 1"));

        Bid(game, 0, 1);

        Assert.Contains(game.AvailableMoves, move =>
            move.From == Square.Parse("e8") && move.To == Square.Parse("e7"));
    }

    [Fact]
    public void MakeMove_TakingTheKing_EndsTheGame()
    {
        var game = NewGame(PieceBoard.FromFen("4k3/8/8/8/8/8/8/4RK2 w - - 0 1"));

        Bid(game, 1, 0);
        game.MakeMove(Square.Parse("e1"), Square.Parse("e8"), null, TimeSpan.Zero);

        Assert.True(game.IsGameOver);
        Assert.Equal(GameEndReason.KingCaptured, game.EndResult!.Value.Reason);
        Assert.Equal(Side.White, game.EndResult!.Value.Winner);
    }

    [Fact]
    public void Resign_HandsTheGameToTheOpponent()
    {
        var game = NewGame();

        game.Resign(Side.White);

        Assert.Equal(new GameEndResult(GameEndReason.Resignation, Side.Black), game.EndResult);
    }

    [Fact]
    public void SidesOnTheClock_WhileBidsAreOpen_AreTheSidesThatHaveNotBidYet()
    {
        var game = NewGame();

        Assert.Equal([Side.White, Side.Black], game.SidesOnTheClock);

        game.SubmitBid(Side.White, 3, TimeSpan.Zero);

        Assert.Equal([Side.Black], game.SidesOnTheClock);
    }

    [Fact]
    public void DeclareTimeoutIfFlagged_WithABidStillOwed_EndsTheGameAgainstThatSide()
    {
        var game = new GameState(new Clock(TimeSpan.FromSeconds(1), TimeSpan.Zero));
        game.SubmitBid(Side.White, 0, TimeSpan.Zero);
        game.Clock.Deduct(Side.Black, TimeSpan.FromSeconds(2));

        game.DeclareTimeoutIfFlagged();

        Assert.Equal(new GameEndResult(GameEndReason.Timeout, Side.White), game.EndResult);
    }
}
