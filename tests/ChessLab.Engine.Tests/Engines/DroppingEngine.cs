using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Engines;

public sealed class DroppingEngine(IEngineUnderTest inner, Func<string, bool> drops) : IEngineUnderTest
{
    public GameKind Kind => inner.Kind;

    public string Name => $"{inner.Name}-dropping";

    public bool AllocationFreeSearch => inner.AllocationFreeSearch;

    public string PositionText => inner.PositionText;

    public ulong Hash => inner.Hash;

    public void Load(PositionLine position) => inner.Load(position);

    public IReadOnlyList<string> LegalActions() => [.. inner.LegalActions().Where(action => !drops(action))];

    public void Apply(string action) => inner.Apply(action);

    public void Undo() => inner.Undo();

    public ulong ComputeHashFromScratch() => inner.ComputeHashFromScratch();

    public int Evaluate() => inner.Evaluate();

    public bool TryMirror() => inner.TryMirror();

    public SearchResult Search(int depth) => inner.Search(depth);
}
