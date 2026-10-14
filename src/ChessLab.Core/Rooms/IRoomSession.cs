using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;

namespace ChessLab.Core.Rooms;

public interface IRoomSession
{
    Room Room { get; }

    IGameEngineState? Game { get; }

    bool HasActiveGame { get; }

    IReadOnlyList<SeatId> ActiveSeats { get; }

    IReadOnlyList<SeatId> SeatsAllowedToAct { get; }

    void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null);

    bool IsPaused { get; }

    DateTimeOffset? PauseDeadline { get; }

    void Apply(GameAction action, SeatId seat, DateTimeOffset now);

    void ApplyBotAction(GameAction action, SeatId seat, DateTimeOffset askedAt, DateTimeOffset now);

    void Pause(DateTimeOffset now);

    bool EndIfPauseExpired(DateTimeOffset now);

    void EndBecauseBotUnavailable();

    void DeclareTimeoutIfExpired(DateTimeOffset now);

    GameStateEnvelopeDto ToStateDto(Side? viewer);

    GameUpdateEnvelopeDto ToUpdateDto(Side? viewer);
}
