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

    public override SeatId ActiveSeat => new(StartedGame.SideToMove, SeatRole.Player);

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        var rng = random ?? Random.Shared;
        Game = new GameState(engineFactory(), new Clock(initial, increment), Deck.Shuffled(rng), Deck.Shuffled(rng));
        TurnStartedAt = now;
    }

    public override void Apply(GameAction action, DateTimeOffset now)
    {
        switch (action.Kind)
        {
            case GameActionKind.Move:
                MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
                break;
            case GameActionKind.SelectReroll:
                SelectCardsForReroll(action.Cards!);
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
                game.HandOf(Side.White),
                game.HandOf(Side.Black),
                game.PendingRerollOf(Side.White),
                game.PendingRerollOf(Side.Black),
                game.EmergencyMoveAvailable,
                game.HandHasNoPlayableCard,
                game.HpOf(Side.White),
                game.HpOf(Side.Black));
        }
    }
}
