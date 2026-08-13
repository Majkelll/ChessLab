using BrainAndHand.Core.Chess;
using BrainAndHand.Core.Contracts;
using BrainAndHand.Core.HandBrain;
using BrainAndHand.Core.Rooms;

namespace BrainAndHand.Web.Hubs;

/// <summary>Shared by GameHub, BotRunner, and ClockWatchdog so they all broadcast the exact same wire shape.</summary>
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

    public static CardChessStateDto ToCardChessDto(CardChessSession session)
    {
        var game = session.Game!;
        return new CardChessStateDto(
            game.SideToMove,
            game.HandOf(Side.White),
            game.HandOf(Side.Black),
            game.EmergencyMoveAvailable,
            game.HandHasNoPlayableCard,
            game.AvailableMoves,
            game.MoveHistory,
            game.ToFen(),
            game.HpOf(Side.White),
            game.HpOf(Side.Black),
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner);
    }
}
