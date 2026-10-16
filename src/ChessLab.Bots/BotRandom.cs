namespace ChessLab.Bots;

internal static class BotRandom
{
    public static Random For(int? seed) => seed is { } value ? new Random(value) : new Random();
}
