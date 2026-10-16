using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Mining;

public sealed record Blunder(TacticPosition Better, string Played, int Loss);

public static class BlunderMiner
{
    public static IEnumerable<Blunder> Find(GameKind kind, IReadOnlyList<ReplayedTurn> turns,
        IReadOnlySet<Side> botSides, MiningSettings settings)
    {
        foreach (var turn in turns.Where(turn => botSides.Contains(turn.Actor)))
        {
            var scores = RootScores(kind, turn.Before, turn.Actor, settings.Depth);
            if (scores.Count < 2 || !scores.TryGetValue(turn.Played[0], out var played))
                continue;

            var best = scores.MaxBy(entry => entry.Value);
            var loss = best.Value - played;
            if (loss >= settings.Threshold)
                yield return new Blunder(new TacticPosition(best.Key, turn.Before), turn.Played[0], loss);
        }
    }

    private static Dictionary<string, int> RootScores(GameKind kind, PositionLine position, Side actor, int depth)
    {
        var engine = EngineRegistry.For(kind);
        engine.Load(position);
        var sign = actor == Side.White ? 1 : -1;
        var prefix = actor == Side.White ? "w:" : "b:";
        var scores = new Dictionary<string, int>();

        foreach (var action in engine.LegalActions().Where(action => action.StartsWith(prefix, StringComparison.Ordinal)))
        {
            engine.Apply(action);
            scores[action] = sign * engine.Search(Math.Max(0, depth - 1)).Score;
            engine.Undo();
        }

        return scores;
    }
}
