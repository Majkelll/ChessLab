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

    HpDepleted,

    KingCaptured,

    BoardHalfEmptied,

    MoveLimit,

    BotUnavailable,
}

public readonly record struct GameEndResult(GameEndReason Reason, Side? Winner);
