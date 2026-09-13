using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.Rooms;

/// <summary>Ties a lobby <see cref="Room"/> to its (possibly not-yet-started) <see cref="GameState"/>.</summary>
public sealed class GameSession(Room room, Func<IChessRulesEngine> engineFactory) : IRoomSession
{
    public Room Room { get; } = room;
    public GameState? Game { get; private set; }
    public DateTimeOffset? TurnStartedAt { get; private set; }
    public bool HasActiveGame => Game is { IsGameOver: false };

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

    /// <summary>
    /// Ends the game by timeout if the side to move has used up its clock, even though nobody has
    /// made a move (which is normally what deducts elapsed time from the clock). <see cref="Clock"/>
    /// only holds the remaining time as of the last move, so a side that simply stops playing would
    /// otherwise never be flagged — call this periodically (e.g. from a background timer) to catch
    /// that case using real elapsed time since the current turn started.
    /// </summary>
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
