using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;
using GameState = ChessLab.Core.CardChess.GameState;

namespace ChessLab.Core.Rooms;

public sealed class CardChessSession(Room room, Func<IChessRulesEngine> engineFactory) : IRoomSession
{
    public Room Room { get; } = room;
    public GameState? Game { get; private set; }
    public DateTimeOffset? TurnStartedAt { get; private set; }
    public bool HasActiveGame => Game is { IsGameOver: false };

    public CardChessSession(Room room) : this(room, static () => new GeraChessRulesEngine())
    {
    }

    public SeatId ActiveSeat
    {
        get
        {
            if (Game is null)
                throw new InvalidOperationException("Game has not started.");

            return new SeatId(Game.SideToMove, SeatRole.Player);
        }
    }

    public void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        if (Game is not null)
            throw new InvalidOperationException("Game has already started.");

        Room.Lock();
        var rng = random ?? Random.Shared;
        Game = new GameState(engineFactory(), new Clock(initial, increment), Deck.Shuffled(rng), Deck.Shuffled(rng));
        TurnStartedAt = now;
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        if (Game is null)
            throw new InvalidOperationException("Game has not started.");

        var elapsed = TurnStartedAt is { } startedAt ? now - startedAt : TimeSpan.Zero;
        var move = Game.MakeMove(from, to, promoteTo, elapsed);
        TurnStartedAt = now;
        return move;
    }

    public void SelectCardsForReroll(IReadOnlyList<CardRank> cards)
    {
        if (Game is null)
            throw new InvalidOperationException("Game has not started.");

        Game.SelectCardsForReroll(cards);
    }

    public void DeclareTimeoutIfExpired(DateTimeOffset now)
    {
        if (Game is not { IsGameOver: false } || TurnStartedAt is not { } startedAt)
            return;

        var elapsedSinceTurnStart = now - startedAt;
        if (elapsedSinceTurnStart >= Game.Clock.Remaining(Game.SideToMove))
            Game.Clock.Deduct(Game.SideToMove, elapsedSinceTurnStart);

        Game.DeclareTimeoutIfFlagged();
    }
}
