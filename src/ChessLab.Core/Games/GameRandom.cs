namespace ChessLab.Core.Games;

public sealed class GameRandom
{
    public const int WhiteCards = 0;
    public const int BlackCards = 1;
    public const int WhiteSpells = 2;
    public const int BlackSpells = 3;
    public const int SpellEffects = 4;

    private ulong state;

    public GameRandom(int seed) : this(unchecked((ulong)seed))
    {
    }

    private GameRandom(ulong state) => this.state = state;

    public static GameRandom Unseeded() => new(unchecked((ulong)Random.Shared.NextInt64()));

    public static GameRandom For(int? seed, int stream) =>
        seed is { } value ? new GameRandom(unchecked((value * 31) + stream)) : Unseeded();

    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return (int)(((NextUInt64() >> 32) * (ulong)maxExclusive) >> 32);
    }

    public GameRandom Clone() => new(state);

    private ulong NextUInt64()
    {
        unchecked
        {
            var z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
