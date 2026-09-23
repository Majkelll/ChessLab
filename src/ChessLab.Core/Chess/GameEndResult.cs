namespace ChessLab.Core.Chess;

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

    /// <summary>Bidding Chess only: there is no check or mate there, so the game is won by taking
    /// the opposing king off the board.</summary>
    KingCaptured,

    /// <summary>Drawn because the mode's cap on how long a game may run was reached. Modes that
    /// reload positions or don't track repetition at all rely on this instead of the usual draws.</summary>
    MoveLimit,
}

/// <summary>Winner is null for draws/stalemate.</summary>
public readonly record struct GameEndResult(GameEndReason Reason, Side? Winner);
