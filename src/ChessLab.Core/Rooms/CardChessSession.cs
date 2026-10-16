using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using GameState = ChessLab.Core.CardChess.GameState;

namespace ChessLab.Core.Rooms;

public sealed class CardChessSession(Room room, Func<IChessRulesEngine> engineFactory) : RoomSession<GameState>(room)
{
    public CardChessSession(Room room) : this(room, static () => new GeraChessRulesEngine())
    {
    }

    public override IReadOnlyList<SeatId> ActiveSeats => [new SeatId(StartedGame.SideToMove, SeatRole.Player)];

    protected override RoomSession<GameState> CreateSibling() => new CardChessSession(Room, engineFactory);

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, int? seed = null)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = GameState.Deal(engineFactory(), new Clock(initial, increment), seed);
        TurnStartedAt = now;
    }

    protected override void ApplyAction(GameAction action, SeatId seat, DateTimeOffset now)
    {
        switch (action.Kind)
        {
            case GameActionKind.Move:
                MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
                break;
            case GameActionKind.SelectReroll:
                SelectCardsForReroll(action.Cards ?? throw new InvalidOperationException("A reroll selection needs a list of cards."));
                break;
            default:
                throw new InvalidOperationException($"{action.Kind} is not an action in Card Chess.");
        }
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, promoteTo, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    public void SelectCardsForReroll(IReadOnlyList<CardRank> cards) => StartedGame.SelectCardsForReroll(cards);

    protected override IReadOnlyList<ChessMove> AvailableMoves => StartedGame.AvailableMoves;

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override CardChessSectionDto CardChessSection
    {
        get
        {
            var game = StartedGame;
            return new CardChessSectionDto(
                [.. game.HandOf(Side.White)],
                [.. game.HandOf(Side.Black)],
                [.. game.PendingRerollOf(Side.White)],
                [.. game.PendingRerollOf(Side.Black)],
                game.EmergencyMoveAvailable,
                game.HandHasNoPlayableCard,
                game.HpOf(Side.White),
                game.HpOf(Side.Black));
        }
    }
}
