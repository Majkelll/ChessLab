using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Suites;

public sealed record TacticCandidate(TacticPosition Tactic, int Gain, int Margin, string PositionText);

public static class TacticFinder
{
    public const int MinimumGain = 300;
    public const int MinimumMargin = 200;

    public static IEnumerable<TacticCandidate> Find(GameKind kind, IEnumerable<PositionLine> starts, int depth,
        int pliesPerGame, int seed)
    {
        var random = new Random(seed);
        foreach (var start in starts)
        {
            var session = CoreGames.Start(kind, start);
            var line = start;

            for (var ply = 0; ply < pliesPerGame && session.HasActiveGame; ply++)
            {
                if (Evaluate(kind, line, depth) is { } candidate)
                {
                    yield return candidate;
                    break;
                }

                var legal = CoreGames.LegalActions(session);
                var chosen = legal[random.Next(legal.Count)];
                CoreGames.Apply(session, chosen);
                line = line.Then(chosen.ToString());
            }
        }
    }

    public static TacticCandidate? Evaluate(GameKind kind, PositionLine line, int depth)
    {
        if (kind == GameKind.ProgressiveChess &&
            CoreGames.Start(kind, line).ToStateDto(null).Progressive is not { MovesLeftInSeries: 1 })
        {
            return null;
        }

        var engine = new CoreReferenceEngine(kind);
        engine.Load(line);
        var legal = engine.LegalActions();
        if (legal.Count < 2 || legal.Select(action => action[0]).Distinct().Count() > 1)
            return null;

        var white = legal[0][0] == 'w';
        var sign = white ? 1 : -1;
        var before = sign * engine.Evaluate();
        var scores = new List<(string Action, int Score)>();
        foreach (var action in legal)
        {
            engine.Apply(action);
            scores.Add((action, sign * engine.Search(depth - 1).Score));
            engine.Undo();
        }

        var ranked = scores.OrderByDescending(entry => entry.Score).ToArray();
        var gain = ranked[0].Score - before;
        var margin = ranked[0].Score - ranked[1].Score;
        return gain >= MinimumGain && margin >= MinimumMargin
            ? new TacticCandidate(new TacticPosition(ranked[0].Action, line), gain, margin, engine.PositionText)
            : null;
    }
}
