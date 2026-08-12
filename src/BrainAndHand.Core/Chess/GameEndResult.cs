namespace BrainAndHand.Core.Chess;

public enum GameEndReason
{
    Checkmate,
    Stalemate,
    InsufficientMaterial,
    FiftyMoveRule,
    Repetition,
    Resignation,
    Timeout,
    DrawAgreed,
}

/// <summary>Winner is null for draws/stalemate.</summary>
public readonly record struct GameEndResult(GameEndReason Reason, Side? Winner);
