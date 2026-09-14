using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Web.Hubs;

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

    public static GameUpdateDto ToGameUpdateDto(GameSession session)
    {
        var game = session.Game!;
        return new GameUpdateDto(
            game.SideToMove,
            game.Phase,
            game.SelectedPieceKind,
            game.Phase == TurnPhase.BrainSelecting && !game.IsGameOver ? game.AvailablePieceKinds() : [],
            game.Phase == TurnPhase.HandMoving && !game.IsGameOver ? game.AvailableMoves() : [],
            game.MoveHistory.Count > 0 ? game.MoveHistory[^1] : null,
            game.MoveHistory.Count,
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
            game.PendingRerollOf(Side.White),
            game.PendingRerollOf(Side.Black),
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

    public static CardChessUpdateDto ToCardChessUpdateDto(CardChessSession session)
    {
        var game = session.Game!;
        return new CardChessUpdateDto(
            game.SideToMove,
            game.HandOf(Side.White),
            game.HandOf(Side.Black),
            game.PendingRerollOf(Side.White),
            game.PendingRerollOf(Side.Black),
            game.EmergencyMoveAvailable,
            game.HandHasNoPlayableCard,
            game.AvailableMoves,
            game.MoveHistory.Count > 0 ? game.MoveHistory[^1] : null,
            game.MoveHistory.Count,
            game.ToFen(),
            game.HpOf(Side.White),
            game.HpOf(Side.Black),
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner);
    }

    public static ArcaneChessStateDto ToArcaneChessDto(ArcaneChessSession session)
    {
        var game = session.Game!;
        return new ArcaneChessStateDto(
            game.SideToMove,
            game.HandOf(Side.White),
            game.HandOf(Side.Black),
            game.PendingRerollOf(Side.White),
            game.PendingRerollOf(Side.Black),
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
            game.EndResult?.Winner,
            game.SpellHandOf(Side.White),
            game.SpellHandOf(Side.Black),
            game.ManaOf(Side.White),
            game.ManaOf(Side.Black),
            game.IsOpponentHandRevealedTo(Side.White),
            game.IsOpponentHandRevealedTo(Side.Black),
            game.ActiveEffects);
    }

    public static ArcaneChessUpdateDto ToArcaneChessUpdateDto(ArcaneChessSession session)
    {
        var game = session.Game!;
        return new ArcaneChessUpdateDto(
            game.SideToMove,
            game.HandOf(Side.White),
            game.HandOf(Side.Black),
            game.PendingRerollOf(Side.White),
            game.PendingRerollOf(Side.Black),
            game.EmergencyMoveAvailable,
            game.HandHasNoPlayableCard,
            game.AvailableMoves,
            game.MoveHistory.Count > 0 ? game.MoveHistory[^1] : null,
            game.MoveHistory.Count,
            game.ToFen(),
            game.HpOf(Side.White),
            game.HpOf(Side.Black),
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner,
            game.SpellHandOf(Side.White),
            game.SpellHandOf(Side.Black),
            game.ManaOf(Side.White),
            game.ManaOf(Side.Black),
            game.IsOpponentHandRevealedTo(Side.White),
            game.IsOpponentHandRevealedTo(Side.Black),
            game.ActiveEffects);
    }
}
