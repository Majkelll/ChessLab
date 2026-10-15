using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;

namespace ChessLab.Arena.Checks;

public sealed record InvariantViolation(string Invariant, PositionLine Position, string Detail)
{
    public string Describe() => $"{Invariant} broken in '{Position}': {Detail}";
}

public static class Invariants
{
    public const string MakeUnmake = "make and unmake";
    public const string IncrementalHash = "incremental hash";
    public const string MirroredEvaluation = "mirrored evaluation";
    public const string SearchAllocation = "allocation-free search";

    private const long AllocationSlackBytes = 512;

    public static IReadOnlyList<InvariantViolation> Check(IEngineUnderTest engine, IEnumerable<PositionLine> positions,
        int seed = 1, int pliesPerPosition = 16)
    {
        var violations = new List<InvariantViolation>();
        var random = new Random(seed);

        foreach (var start in positions)
        {
            engine.Load(start);
            var line = start;

            for (var ply = 0; ply <= pliesPerPosition; ply++)
            {
                CheckNode(engine, line, violations);
                var actions = engine.LegalActions();
                if (actions.Count == 0)
                    break;

                var chosen = actions[random.Next(actions.Count)];
                engine.Apply(chosen);
                line = line.Then(chosen);
            }
        }

        return violations;
    }

    public static InvariantViolation? CheckSearchAllocation(IEngineUnderTest engine, PositionLine position, int depth)
    {
        engine.Load(position);
        engine.Search(depth);
        engine.Search(depth + 1);

        var shallow = AllocatedBy(() => engine.Search(depth));
        var deep = AllocatedBy(() => engine.Search(depth + 1));

        return deep.Bytes > shallow.Bytes + AllocationSlackBytes
            ? new InvariantViolation(SearchAllocation, position,
                $"depth {depth} allocated {shallow.Bytes} bytes for {shallow.Nodes} nodes, " +
                $"depth {depth + 1} allocated {deep.Bytes} bytes for {deep.Nodes} nodes")
            : null;
    }

    private static (long Bytes, long Nodes) AllocatedBy(Func<SearchResult> search)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = search();
        return (GC.GetAllocatedBytesForCurrentThread() - before, result.Nodes);
    }

    private static void CheckNode(IEngineUnderTest engine, PositionLine line, List<InvariantViolation> violations)
    {
        var scratch = engine.ComputeHashFromScratch();
        if (scratch != engine.Hash)
            violations.Add(new InvariantViolation(IncrementalHash, line,
                $"incremental hash {engine.Hash:x16} but {scratch:x16} from scratch"));

        var before = Snapshot(engine);
        foreach (var action in engine.LegalActions())
        {
            engine.Apply(action);
            engine.Undo();
            var after = Snapshot(engine);
            if (after != before)
                violations.Add(new InvariantViolation(MakeUnmake, line,
                    $"after {action} and undo the state is '{after}' instead of '{before}'"));
        }

        var evaluation = engine.Evaluate();
        if (!engine.TryMirror())
            return;

        var mirrored = engine.Evaluate();
        engine.TryMirror();
        if (mirrored != -evaluation)
            violations.Add(new InvariantViolation(MirroredEvaluation, line,
                $"evaluation {evaluation} but {mirrored} for the mirrored position"));
    }

    private static string Snapshot(IEngineUnderTest engine) =>
        $"{engine.PositionText} | {engine.Hash:x16} | {engine.Evaluate()} | " +
        string.Join(' ', engine.LegalActions().Order(StringComparer.Ordinal));
}
