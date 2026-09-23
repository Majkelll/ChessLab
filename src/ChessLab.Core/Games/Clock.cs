using ChessLab.Core.Chess;

namespace ChessLab.Core.Games;

/// <summary>Per-side chess clock with a Fischer increment. Pure data — ticking is driven externally.</summary>
public sealed class Clock
{
    public TimeSpan Increment { get; }
    public TimeSpan WhiteRemaining { get; private set; }
    public TimeSpan BlackRemaining { get; private set; }

    public Clock(TimeSpan initial, TimeSpan increment)
    {
        Increment = increment;
        WhiteRemaining = initial;
        BlackRemaining = initial;
    }

    public TimeSpan Remaining(Side side) => side == Side.White ? WhiteRemaining : BlackRemaining;

    public bool IsFlagged(Side side) => Remaining(side) <= TimeSpan.Zero;

    public void Deduct(Side side, TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));

        if (side == Side.White)
            WhiteRemaining -= elapsed;
        else
            BlackRemaining -= elapsed;
    }

    public void ApplyIncrement(Side side)
    {
        if (side == Side.White)
            WhiteRemaining += Increment;
        else
            BlackRemaining += Increment;
    }
}
