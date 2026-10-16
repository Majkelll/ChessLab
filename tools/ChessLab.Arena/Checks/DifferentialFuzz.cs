using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Checks;

public sealed record FuzzFailure(
    GameKind Kind,
    int Seed,
    PositionLine Position,
    string PositionText,
    IReadOnlyList<string> MissingFromEngine,
    IReadOnlyList<string> ExtraInEngine)
{
    public string Describe() => string.Join(Environment.NewLine,
        $"mode: {Modes.FolderOf(Kind)}",
        $"seed: {Seed}",
        $"position: {Position}",
        $"position text: {PositionText}",
        $"missing from the engine: {string.Join(' ', MissingFromEngine)}",
        $"not legal in Core: {string.Join(' ', ExtraInEngine)}");
}

public static class DifferentialFuzz
{
    public const int DefaultActionLimit = 400;

    public static FuzzFailure? Run(IEngineUnderTest engine, int seed, int actionLimit = DefaultActionLimit)
    {
        var kind = engine.Kind;
        var line = PositionLine.Start(seed);
        var session = CoreGames.Start(kind, line);
        engine.Load(line);
        var random = new Random(seed);

        for (var step = 0; step <= actionLimit; step++)
        {
            var coreActions = CoreGames.LegalActions(session).Select(ActionNotation.Format)
                .Order(StringComparer.Ordinal).ToArray();
            var engineActions = engine.LegalActions().Order(StringComparer.Ordinal).ToArray();

            if (!coreActions.SequenceEqual(engineActions))
                return new FuzzFailure(kind, seed, line, session.Game!.PositionText,
                    [.. coreActions.Except(engineActions)], [.. engineActions.Except(coreActions)]);

            if (coreActions.Length == 0)
                return null;

            var chosen = coreActions[random.Next(coreActions.Length)];
            CoreGames.Apply(session, ActionNotation.Parse(chosen, kind));
            engine.Apply(chosen);
            line = line.Then(chosen);
        }

        return null;
    }
}
