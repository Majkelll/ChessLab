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

    void Apply(GameAction action, SeatId seat, DateTimeOffset now);

    void DeclareTimeoutIfExpired(DateTimeOffset now);

    GameStateEnvelopeDto ToStateDto(Side? viewer);

    GameUpdateEnvelopeDto ToUpdateDto(Side? viewer);
}
