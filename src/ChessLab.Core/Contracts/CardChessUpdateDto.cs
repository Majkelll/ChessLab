using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;

namespace ChessLab.Core.Contracts;

public sealed record CardChessUpdateDto(
    Side SideToMove,
    IReadOnlyList<CardRank> WhiteHand,
    IReadOnlyList<CardRank> BlackHand,
    IReadOnlyList<CardRank> WhitePendingReroll,
    IReadOnlyList<CardRank> BlackPendingReroll,
    bool EmergencyMoveAvailable,
    bool HandHasNoPlayableCard,
    IReadOnlyList<ChessMove> AvailableMoves,
    ChessMove? LatestMove,
    int MoveHistoryCount,
    string Fen,
    int WhiteHp,
    int BlackHp,
    long WhiteRemainingMs,
    long BlackRemainingMs,
    bool IsGameOver,
    GameEndReason? EndReason,
    Side? Winner);
