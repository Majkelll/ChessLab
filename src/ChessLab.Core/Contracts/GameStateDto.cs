using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.Contracts;

public sealed record GameStateDto(
    Side SideToMove,
    TurnPhase Phase,
    PieceKind? SelectedPieceKind,
    IReadOnlyList<PieceKind> AvailablePieceKinds,
    IReadOnlyList<ChessMove> AvailableMoves,
    IReadOnlyList<ChessMove> MoveHistory,
    string Fen,
    long WhiteRemainingMs,
    long BlackRemainingMs,
    bool IsGameOver,
    GameEndReason? EndReason,
    Side? Winner);
