using ChessLab.Bots;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Bots;

public class DraftBotTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static DraftChessSession StartedDraft()
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), GameKind.DraftChess);
        room.SetBot(White, BotDifficulty.Hard);
        room.ClaimSeat(Black, Guid.NewGuid(), "Bob");
        var session = new DraftChessSession(room);
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);
        return session;
    }

    private static Task<GameAction> ActAsync(IRoomSession session, BotDifficulty difficulty = BotDifficulty.Hard) =>
        new DraftChessBot().ChooseActionAsync(session.ToStateDto(Side.White), White, difficulty);

    [Fact]
    public async Task Drafting_TakesTheMostValuableKindItMayPick()
    {
        var session = StartedDraft();

        Assert.Equal(GameAction.DraftPick(PieceKind.Queen), await ActAsync(session));
    }

    [Fact]
    public async Task Drafting_SkipsKindsItCannotAfford()
    {
        var state = StartedDraft().ToStateDto(Side.White);
        state = state with { Draft = state.Draft! with { WhiteBudgetLeft = 4 } };

        var action = await new DraftChessBot().ChooseActionAsync(state, White, BotDifficulty.Expert);

        Assert.Equal(GameAction.DraftPick(PieceKind.Bishop), action);
    }

    [Fact]
    public async Task Drafting_SkipsKindsTheBotHasTooManyOf()
    {
        var state = StartedDraft().ToStateDto(Side.White);
        state = state with
        {
            Draft = state.Draft! with
            {
                WhiteBudgetLeft = 1,
                WhitePicks = [.. Enumerable.Repeat(PieceKind.Pawn, GameState.MaxPawns)],
            },
        };

        Assert.Equal(GameAction.DraftPass(), await new DraftChessBot().ChooseActionAsync(state, White, BotDifficulty.Hard));
    }

    [Fact]
    public async Task Drafting_WithNothingAffordable_Passes()
    {
        var state = StartedDraft().ToStateDto(Side.White);
        state = state with { Draft = state.Draft! with { WhiteBudgetLeft = 0 } };

        Assert.Equal(GameAction.DraftPass(), await new DraftChessBot().ChooseActionAsync(state, White, BotDifficulty.Hard));
    }

    [Fact]
    public async Task Placing_PutsTheKingFirstThenOfficersThenPawnsInFileOrder()
    {
        var session = StartedDraft();
        session.ApplyBotAction(GameAction.DraftPick(PieceKind.Queen), White, Start, Start);
        session.Apply(GameAction.DraftPass(), Black, Start);
        session.ApplyBotAction(GameAction.DraftPick(PieceKind.Rook), White, Start, Start);
        session.ApplyBotAction(GameAction.DraftPick(PieceKind.Pawn), White, Start, Start);
        session.ApplyBotAction(GameAction.DraftPick(PieceKind.Pawn), White, Start, Start);
        while (session.Game!.Phase == DraftPhase.Drafting)
            session.ApplyBotAction(GameAction.DraftPass(), White, Start, Start);

        var placed = new List<GameAction>();
        while (!session.Game.HasFinishedPlacing(Side.White))
        {
            var action = await ActAsync(session);
            session.Apply(action, White, Start);
            placed.Add(action);
        }

        Assert.Equal(
        [
            GameAction.PlacePiece(PieceKind.King, Square.Parse("e1")),
            GameAction.PlacePiece(PieceKind.Queen, Square.Parse("d1")),
            GameAction.PlacePiece(PieceKind.Rook, Square.Parse("f1")),
            GameAction.PlacePiece(PieceKind.Pawn, Square.Parse("e2")),
            GameAction.PlacePiece(PieceKind.Pawn, Square.Parse("d2")),
        ], placed);
    }
}
