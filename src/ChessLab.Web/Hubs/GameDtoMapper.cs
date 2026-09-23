using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.Web.Hubs;

/// <summary>Shared by GameHub and the lobby so they all broadcast the exact same wire shape. The
/// game state itself is mapped by the session — only the room around it is mapped here.</summary>
internal static class GameDtoMapper
{
    public static RoomStateDto ToRoomDto(IRoomSession session) => new(
        session.Room.Code,
        session.Room.HostUserId,
        session.Room.Kind,
        session.Room.SeatIds.Select(id =>
        {
            var occupant = session.Room.Seats[id];
            return new SeatSnapshot(id, occupant.Kind, occupant.UserId, occupant.DisplayName, occupant.Difficulty);
        }).ToArray(),
        session.HasActiveGame,
        session.Room.IsLocked,
        (int)session.Room.InitialClock.TotalSeconds,
        (int)session.Room.ClockIncrement.TotalSeconds);
}
