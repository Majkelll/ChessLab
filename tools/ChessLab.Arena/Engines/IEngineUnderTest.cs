using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Engines;

public readonly record struct SearchResult(long Nodes, string? BestAction, int Score);

public interface IEngineUnderTest
{
    GameKind Kind { get; }

    string Name { get; }

    bool AllocationFreeSearch { get; }

    string PositionText { get; }

    ulong Hash { get; }

    void Load(PositionLine position);

    IReadOnlyList<string> LegalActions();

    void Apply(string action);

    void Undo();

    ulong ComputeHashFromScratch();

    int Evaluate();

    bool TryMirror();

    SearchResult Search(int depth);
}
