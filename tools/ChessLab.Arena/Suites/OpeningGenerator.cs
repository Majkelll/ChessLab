using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Suites;

public static class OpeningGenerator
{
    public const int BalanceLimit = 150;

    public static int DefaultPlies(GameKind kind) => kind switch
    {
        GameKind.HandAndBrain => 12,
        GameKind.BiddingChess => 18,
        GameKind.ProgressiveChess => 6,
        GameKind.MartianChess => 6,
        GameKind.DraftChess => 4,
        _ => 8,
    };

    public static IReadOnlyList<PositionLine> Generate(GameKind kind, int count, int plies, int seed,
        int maxAttempts = 5000)
    {
        var openings = new List<PositionLine>();
        var seen = new HashSet<string>();

        for (var attempt = 0; attempt < maxAttempts && openings.Count < count; attempt++)
        {
            var random = new Random(unchecked((seed * 7919) + attempt));
            var line = RandomPlayout(kind, PositionLine.Start(attempt), plies, random);
            if (line is null || !seen.Add(line.ToString()) || !IsBalanced(kind, line))
                continue;

            openings.Add(line);
        }

        if (openings.Count < count)
            throw new InvalidOperationException(
                $"Only {openings.Count} of {count} balanced {Modes.FolderOf(kind)} openings found.");

        return openings;
    }

    public static bool IsBalanced(GameKind kind, PositionLine line)
    {
        var session = CoreGames.Start(kind, line);
        if (!session.HasActiveGame || Math.Abs(MaterialEvaluation.Of(session)) > BalanceLimit / 2)
            return false;

        if (kind == GameKind.BiddingChess)
            return true;

        var engine = new CoreReferenceEngine(kind);
        engine.Load(line);
        return Math.Abs(engine.Search(2).Score) <= BalanceLimit;
    }

    private static PositionLine? RandomPlayout(GameKind kind, PositionLine start, int plies, Random random)
    {
        var session = CoreGames.Start(kind, start);
        var line = start;

        for (var ply = 0; ply < plies; ply++)
        {
            var legal = CoreGames.LegalActions(session);
            if (legal.Count == 0)
                return null;

            var actions = legal
                .Where(action => action.Seat == legal[0].Seat)
                .Where(action => action.Action is not ({ Kind: GameActionKind.SubmitBid, Amount: > 10 }
                    or { Kind: GameActionKind.DraftPass }))
                .ToArray();

            var chosen = actions[random.Next(actions.Length)];
            CoreGames.Apply(session, chosen);
            line = line.Then(chosen.ToString());
        }

        return session.HasActiveGame ? line : null;
    }
}
