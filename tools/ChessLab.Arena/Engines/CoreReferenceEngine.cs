using System.Text.Json;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Engines;

public sealed class CoreReferenceEngine(GameKind kind) : IEngineUnderTest
{
    private readonly Stack<(IRoomSession Session, ulong Hash)> history = new();
    private IRoomSession? session;
    private ulong hash;

    public GameKind Kind => kind;

    public string Name => "core-reference";

    public bool AllocationFreeSearch => false;

    public string PositionText => Current.Game!.PositionText;

    public ulong Hash => hash;

    private IRoomSession Current => session ?? throw new InvalidOperationException("Load a position first.");

    public void Load(PositionLine position)
    {
        session = CoreGames.Start(kind, position);
        history.Clear();
        hash = ComputeHashFromScratch();
    }

    public IReadOnlyList<string> LegalActions() => [.. CoreGames.LegalActions(Current).Select(ActionNotation.Format)];

    public void Apply(string action)
    {
        var next = Current.Clone();
        CoreGames.Apply(next, ActionNotation.Parse(action, kind));
        history.Push((Current, hash));
        session = next;
        hash = ComputeHashFromScratch();
    }

    public void Undo()
    {
        if (!history.TryPop(out var previous))
            throw new InvalidOperationException("There is nothing to undo.");

        (session, hash) = previous;
    }

    public ulong ComputeHashFromScratch() => Fnv.Hash(
        JsonSerializer.Serialize(Current.ToStateDto(Side.White)) + JsonSerializer.Serialize(Current.ToStateDto(Side.Black)));

    public int Evaluate() => MaterialEvaluation.Of(Current);

    public bool TryMirror() => false;

    public SearchResult Search(int depth)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var nodes = 0L;
        var (score, best) = Minimax(depth, -MaterialEvaluation.Win - 1, MaterialEvaluation.Win + 1, ref nodes);
        var spent = System.Diagnostics.Stopwatch.GetElapsedTime(started);
        while (System.Diagnostics.Stopwatch.GetElapsedTime(started) < spent * 1.25) { }
        return new SearchResult(nodes, best, score);
    }

    private (int Score, string? Best) Minimax(int depth, int alpha, int beta, ref long nodes)
    {
        nodes++;
        var actions = ActionsOfTheSeatToAct();
        if (depth == 0 || actions.Count == 0)
            return (Evaluate(), null);

        var maximizing = actions[0].StartsWith("w:", StringComparison.Ordinal);
        var bestScore = maximizing ? int.MinValue : int.MaxValue;
        string? best = null;

        foreach (var action in actions)
        {
            Apply(action);
            var (score, _) = Minimax(depth - 1, alpha, beta, ref nodes);
            Undo();

            if (maximizing ? score > bestScore : score < bestScore)
            {
                bestScore = score;
                best = action;
            }

            if (maximizing)
                alpha = Math.Max(alpha, score);
            else
                beta = Math.Min(beta, score);

            if (alpha >= beta)
                break;
        }

        return (bestScore, best);
    }

    private IReadOnlyList<string> ActionsOfTheSeatToAct()
    {
        var all = LegalActions();
        return all.Count == 0 ? all : [.. all.Where(action => action[0] == all[0][0])];
    }
}
