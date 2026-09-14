using ChessLab.Core.Chess;

namespace ChessLab.Core.ArcaneChess;

public enum ArcaneEffectKind
{
    Shield,
    Freeze,
    PinDown,
    Disarm,
}

/// <summary>A one-turn restriction placed on <see cref="AffectedSide"/>'s upcoming
/// <see cref="GameState.AvailableMoves"/>, still in effect until that side finishes their turn.</summary>
public readonly record struct ArcaneEffect(Side AffectedSide, ArcaneEffectKind Kind, Square Square);
