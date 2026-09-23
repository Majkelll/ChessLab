using ChessLab.Core.Chess;

namespace ChessLab.Core.ArcaneChess;

public enum ArcaneEffectKind
{
    Shield,
    Freeze,
    PinDown,
    Disarm,
}

public readonly record struct ArcaneEffect(Side AffectedSide, ArcaneEffectKind Kind, Square Square);
