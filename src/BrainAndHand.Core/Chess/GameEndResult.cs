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

    /// <summary>Card Chess only: a side needed an Emergency Move while already at 0 HP.</summary>
    HpDepleted,
}

/// <summary>Winner is null for draws/stalemate.</summary>
public readonly record struct GameEndResult(GameEndReason Reason, Side? Winner);
