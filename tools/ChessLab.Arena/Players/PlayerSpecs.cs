using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Players;

public static class PlayerSpecs
{
    public const string Help = "bot:<Easy|Medium|Hard|Expert>, random, or anchor:<directory>:<difficulty>";

    public static IPlayerFactory Parse(string spec, ArenaSettings settings)
    {
        if (spec == "random")
            return new RandomPlayerFactory(settings);

        if (spec.StartsWith("bot:", StringComparison.Ordinal))
            return new BotPlayerFactory(ParseDifficulty(spec["bot:".Length..], spec), settings);

        if (spec.StartsWith("anchor:", StringComparison.Ordinal))
        {
            var rest = spec["anchor:".Length..];
            var colon = rest.LastIndexOf(':');
            if (colon <= 0)
                throw new ArgumentException($"'{spec}' needs a directory and a difficulty, like anchor:/path:Expert.");

            return new AnchorPlayerFactory(rest[..colon], ParseDifficulty(rest[(colon + 1)..], spec), settings);
        }

        throw new ArgumentException($"Unknown player '{spec}'. Use {Help}.");
    }

    private static BotDifficulty ParseDifficulty(string text, string spec) =>
        Enum.TryParse<BotDifficulty>(text, ignoreCase: true, out var difficulty)
            ? difficulty
            : throw new ArgumentException($"'{spec}' has an unknown difficulty '{text}'.");
}
