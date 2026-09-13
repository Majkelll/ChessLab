using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Contracts;

public sealed record GameHistoryPlayerDto(
    Side Side,
    SeatRole Role,
    Guid? UserId,
    string? DisplayName,
    BotDifficulty? BotDifficulty);

public sealed record GameHistoryEntryDto(
    Guid Id,
    string RoomCode,
    GameKind Kind,
    DateTime FinishedAtUtc,
    GameEndReason EndReason,
    Side? Winner,
    int MoveCount,
    IReadOnlyList<GameHistoryPlayerDto> Players);

public sealed record GameHistoryDetailDto(
    GameHistoryEntryDto Summary,
    string FinalFen,
    IReadOnlyList<string> MovesSan);
