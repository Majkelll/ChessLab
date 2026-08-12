using BrainAndHand.Core.Chess;
using BrainAndHand.Core.HandBrain;

namespace BrainAndHand.Core.Contracts;

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
