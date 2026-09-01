using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;

namespace ChessLab.Core.Contracts;

public sealed record CardChessStateDto(
    Side SideToMove,
    IReadOnlyList<CardRank> WhiteHand,
    IReadOnlyList<CardRank> BlackHand,
    /// <summary>True when the side to move is in check and no hand card can escape it — the one
    /// case where playing one of <see cref="AvailableMoves"/> costs 1 HP.</summary>
    bool EmergencyMoveAvailable,
    /// <summary>True whenever no hand card has a legal move at all, in or out of check —
    /// <see cref="AvailableMoves"/> falls back to every legal move either way, but it only costs HP
    /// alongside <see cref="EmergencyMoveAvailable"/>.</summary>
    bool HandHasNoPlayableCard,
    IReadOnlyList<ChessMove> AvailableMoves,
    IReadOnlyList<ChessMove> MoveHistory,
    string Fen,
    int WhiteHp,
    int BlackHp,
    long WhiteRemainingMs,
    long BlackRemainingMs,
    bool IsGameOver,
    GameEndReason? EndReason,
    Side? Winner);
