using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;

namespace ChessLab.Arena.Checks;

public sealed record PerftMismatch(
    PositionLine Position,
    int Depth,
    string? Action,
    long EngineNodes,
    long ReferenceNodes,
    IReadOnlyList<string> MissingFromEngine,
    IReadOnlyList<string> ExtraInEngine)
{
    public string Describe()
    {
        var where = Action is null
            ? $"at depth {Depth} the legal actions differ"
            : $"the subtree of {Action} at depth {Depth} has {EngineNodes} nodes instead of {ReferenceNodes}";
        var lines = new List<string> { $"Perft mismatch in position '{Position}': {where}." };
        if (MissingFromEngine.Count > 0)
            lines.Add($"  Missing from the engine: {string.Join(' ', MissingFromEngine)}");
        if (ExtraInEngine.Count > 0)
            lines.Add($"  Not legal in Core: {string.Join(' ', ExtraInEngine)}");

        return string.Join(Environment.NewLine, lines);
    }
}

public static class Perft
{
    public static long Count(IEngineUnderTest engine, int depth)
    {
        if (depth == 0)
            return 1;

        var actions = engine.LegalActions();
        if (depth == 1)
            return actions.Count;

        var nodes = 0L;
        foreach (var action in actions)
        {
            engine.Apply(action);
            nodes += Count(engine, depth - 1);
            engine.Undo();
        }

        return nodes;
    }

    public static long Count(IEngineUnderTest engine, PositionLine position, int depth)
    {
        engine.Load(position);
        return Count(engine, depth);
    }

    public static PerftMismatch? FirstDifference(IEngineUnderTest engine, IEngineUnderTest reference,
        PositionLine position, int depth)
    {
        engine.Load(position);
        reference.Load(position);
        return Compare(engine, reference, position, depth);
    }

    private static PerftMismatch? Compare(IEngineUnderTest engine, IEngineUnderTest reference,
        PositionLine position, int depth)
    {
        var engineActions = engine.LegalActions().Order(StringComparer.Ordinal).ToArray();
        var referenceActions = reference.LegalActions().Order(StringComparer.Ordinal).ToArray();

        if (!engineActions.SequenceEqual(referenceActions))
            return new PerftMismatch(position, depth, null, engineActions.Length, referenceActions.Length,
                [.. referenceActions.Except(engineActions)], [.. engineActions.Except(referenceActions)]);

        if (depth <= 1)
            return null;

        foreach (var action in referenceActions)
        {
            engine.Apply(action);
            reference.Apply(action);
            var engineNodes = Count(engine, depth - 1);
            var referenceNodes = Count(reference, depth - 1);

            if (engineNodes != referenceNodes)
            {
                var deeper = Compare(engine, reference, position.Then(action), depth - 1);
                engine.Undo();
                reference.Undo();
                return deeper ?? new PerftMismatch(position, depth, action, engineNodes, referenceNodes, [], []);
            }

            engine.Undo();
            reference.Undo();
        }

        return null;
    }
}
