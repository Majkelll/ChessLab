using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.Contracts;

public sealed record GameUpdateDto(
    Side SideToMove,
    TurnPhase Phase,
    PieceKind? SelectedPieceKind,
    IReadOnlyList<PieceKind> AvailablePieceKinds,
    IReadOnlyList<ChessMove> AvailableMoves,
    ChessMove? LatestMove,
    int MoveHistoryCount,
    string Fen,
    long WhiteRemainingMs,
    long BlackRemainingMs,
    bool IsGameOver,
    GameEndReason? EndReason,
    Side? Winner);
