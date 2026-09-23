using ChessLab.Core.Contracts;
using ChessLab.Core.Games;

namespace ChessLab.Core.Rooms;

/// <summary>A room plus the game being played in it, in the one shape the hub, the bot runner, the
/// clock watchdog and the archive all talk to — none of them knows which game kind they're holding.</summary>
public interface IRoomSession
{
    Room Room { get; }

    /// <summary>Null until the game is started.</summary>
    IGameEngineState? Game { get; }

    /// <summary>True once a game has started and is still in progress.</summary>
    bool HasActiveGame { get; }

    /// <summary>The seats whose occupants may act right now — more than one only in a mode where
    /// both sides act at the same time, such as Bidding Chess's sealed bids.</summary>
    IReadOnlyList<SeatId> ActiveSeats { get; }

    void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null);

    void Apply(GameAction action, SeatId seat, DateTimeOffset now);

    /// <summary>Ends the game by timeout if the side to move has used up its clock, even though
    /// nobody has made a move (which is normally what deducts elapsed time from the clock). The
    /// clock only holds the remaining time as of the last move, so a side that simply stops playing
    /// would otherwise never be flagged — call this periodically from a background timer.</summary>
    void DeclareTimeoutIfExpired(DateTimeOffset now);

    GameStateEnvelopeDto ToStateDto();

    GameUpdateEnvelopeDto ToUpdateDto();
}
