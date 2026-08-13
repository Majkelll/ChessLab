using BrainAndHand.Core.Contracts;
using BrainAndHand.Core.HandBrain;
using BrainAndHand.Core.Rooms;

namespace BrainAndHand.Web.Hubs;

/// <summary>Shared by GameHub and BotRunner so both broadcast the exact same wire shape.</summary>
internal static class GameDtoMapper
{
    public static RoomStateDto ToRoomDto(GameSession session) => new(
        session.Room.Code,
        session.Room.HostUserId,
        Room.AllSeatIds.Select(id =>
        {
            var occupant = session.Room.Seats[id];
            return new SeatSnapshot(id, occupant.Kind, occupant.UserId, occupant.DisplayName, occupant.Difficulty);
        }).ToArray(),
        session.Game is not null,
        (int)session.Room.InitialClock.TotalSeconds,
        (int)session.Room.ClockIncrement.TotalSeconds);

    public static GameStateDto ToGameDto(GameSession session)
    {
        var game = session.Game!;
        return new GameStateDto(
            game.SideToMove,
            game.Phase,
            game.SelectedPieceKind,
            game.Phase == TurnPhase.BrainSelecting && !game.IsGameOver ? game.AvailablePieceKinds() : [],
            game.Phase == TurnPhase.HandMoving && !game.IsGameOver ? game.AvailableMoves() : [],
            game.MoveHistory,
            game.ToFen(),
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner);
    }
}
