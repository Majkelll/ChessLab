using ChessLab.Bots;
using ChessLab.Core.Bidding;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Bots;

public class BiddingBotTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);

    private static GameStateEnvelopeDto BiddingState(string fen, int whiteChips = 100) =>
        new(GameKind.BiddingChess, Side.White, fen, [], [], 600_000, 600_000, false, null, null,
            Bidding: new BiddingSectionDto(BiddingPhase.Bidding, whiteChips, 200 - whiteChips, Side.White,
                false, false, null, null));

    private static async Task<int> BidAsync(GameStateEnvelopeDto state, BotDifficulty difficulty)
    {
        var action = await new BiddingChessBot().ChooseActionAsync(state, White, difficulty);
        Assert.Equal(GameActionKind.SubmitBid, action.Kind);
        return action.Amount!.Value;
    }

    [Fact]
    public async Task WithNoChips_BidsZero() =>
        Assert.Equal(0, await BidAsync(BiddingState("4k2R/8/8/8/8/8/8/4K3 w - - 0 1", whiteChips: 0), BotDifficulty.Hard));

    [Fact]
    public async Task OnEasy_BidsAtMostTwenty()
    {
        for (var i = 0; i < 40; i++)
            Assert.InRange(await BidAsync(BiddingState("4k2R/8/8/8/8/8/8/4K3 w - - 0 1"), BotDifficulty.Easy), 0, 20);
    }

    [Fact]
    public async Task WhenItCanTakeTheKing_BidsEverything() =>
        Assert.Equal(100, await BidAsync(BiddingState("4k2R/8/8/8/8/8/8/4K3 b - - 0 1"), BotDifficulty.Medium));

    [Fact]
    public async Task WhenTheOpponentCanTakeItsKing_BidsThreeQuarters() =>
        Assert.Equal(60, await BidAsync(BiddingState("4k3/8/8/8/8/8/8/r3K3 w - - 0 1", whiteChips: 80), BotDifficulty.Hard));

    [Fact]
    public async Task OrdinaryBid_IsTwoPlusThreeTimesTheBestCaptureMinusUpToTwo()
    {
        for (var i = 0; i < 20; i++)
            Assert.InRange(await BidAsync(BiddingState("4k3/8/8/3q4/4P3/8/8/4K3 w - - 0 1"), BotDifficulty.Expert), 27, 29);
    }

    [Fact]
    public async Task OrdinaryBid_IsCappedByTheChips()
    {
        for (var i = 0; i < 20; i++)
            Assert.InRange(await BidAsync(BiddingState("4k3/8/8/3q4/4P3/8/8/4K3 w - - 0 1", whiteChips: 10), BotDifficulty.Expert), 8, 10);
    }

    [Fact]
    public async Task InTheMovingPhase_PlaysTheBestCapture()
    {
        var capture = new ChessMove(Square.Parse("e4"), Square.Parse("d5"), PieceKind.Pawn, PieceKind.Queen, null, false, false, "exd5");
        var quiet = new ChessMove(Square.Parse("e4"), Square.Parse("e5"), PieceKind.Pawn, null, null, false, false, "e5");
        var state = BiddingState("4k3/8/8/3q4/4P3/8/8/4K3 w - - 0 1") with
        {
            AvailableMoves = [quiet, capture],
            Bidding = new BiddingSectionDto(BiddingPhase.Moving, 100, 100, Side.White, false, false, 5, 3),
        };

        var action = await new BiddingChessBot().ChooseActionAsync(state, White, BotDifficulty.Hard);

        Assert.Equal(GameAction.MovePiece(capture.From, capture.To), action);
    }
}
