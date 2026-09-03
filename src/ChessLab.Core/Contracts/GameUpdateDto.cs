using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.Contracts;

/// <summary>Lighter-weight sibling of <see cref="GameStateDto"/> used for the SignalR push
/// broadcast on every move/update — carries only the newest move instead of the whole growing
/// history, so per-broadcast payload stays flat instead of growing with the game. Clients that
/// need the full history (initial join, resync after a gap) use <see cref="GameStateDto"/> via
/// GetGameState instead.</summary>
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
