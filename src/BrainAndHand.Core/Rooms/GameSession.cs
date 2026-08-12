using BrainAndHand.Core.Chess;
using BrainAndHand.Core.HandBrain;

namespace BrainAndHand.Core.Rooms;

/// <summary>Ties a lobby <see cref="Room"/> to its (possibly not-yet-started) <see cref="GameState"/>.</summary>
public sealed class GameSession(Room room, Func<IChessRulesEngine> engineFactory)
{
    public Room Room { get; } = room;
    public GameState? Game { get; private set; }
    public DateTimeOffset? TurnStartedAt { get; private set; }

    public GameSession(Room room) : this(room, static () => new GeraChessRulesEngine())
    {
    }

    /// <summary>The seat whose occupant is expected to act right now (Brain to announce, or Hand to move).</summary>
    public SeatId ActiveSeat
    {
        get
        {
            if (Game is null)
                throw new InvalidOperationException("Game has not started.");

            var role = Game.Phase == TurnPhase.BrainSelecting ? SeatRole.Brain : SeatRole.Hand;
            return new SeatId(Game.SideToMove, role);
        }
    }

    public void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now)
    {
        if (Game is not null)
            throw new InvalidOperationException("Game has already started.");

        Room.Lock();
        Game = new GameState(engineFactory(), new Clock(initial, increment));
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
}
