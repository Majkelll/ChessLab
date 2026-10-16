using System.Text;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Statistics;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Matches;

public sealed class MatchReport(GameKind kind, string candidate, string baseline)
{
    private readonly List<PairResult> pairs = [];
    private readonly Dictionary<(string Player, Forfeit Forfeit), int> forfeits = [];

    public GameKind Kind { get; } = kind;

    public string Candidate { get; } = candidate;

    public string Baseline { get; } = baseline;

    public IReadOnlyList<PairResult> Pairs => pairs;

    public long[] Pentanomial { get; } = new long[5];

    public int Wins { get; private set; }

    public int Draws { get; private set; }

    public int Losses { get; private set; }

    public int Games => Wins + Draws + Losses;

    public double Score => Games == 0 ? 0 : (Wins + (0.5 * Draws)) / Games;

    public int ForfeitsOf(string player, Forfeit forfeit) => forfeits.GetValueOrDefault((player, forfeit));

    public void Add(PairResult pair)
    {
        pairs.Add(pair);
        Pentanomial[pair.PentanomialIndex]++;
        Count(pair.CandidateWhite, Side.White);
        Count(pair.CandidateBlack, Side.Black);
    }

    private void Count(GameRecord game, Side candidateSide)
    {
        var points = game.PointsFor(candidateSide);
        if (points == 1)
            Wins++;
        else if (points == 0)
            Losses++;
        else
            Draws++;

        if (game.Forfeit is { } forfeit && game.Winner is { } winner)
        {
            var forfeiter = winner == candidateSide ? Baseline : Candidate;
            forfeits[(forfeiter, forfeit)] = forfeits.GetValueOrDefault((forfeiter, forfeit)) + 1;
        }
    }

    public string Describe()
    {
        var text = new StringBuilder();
        text.AppendLine($"Mode: {Modes.FolderOf(Kind)}");
        text.AppendLine($"Candidate: {Candidate}  Baseline: {Baseline}");
        text.AppendLine($"Games: {Games}  W: {Wins}  D: {Draws}  L: {Losses}  Score: {Score:P1}");
        text.AppendLine($"Pentanomial (LL, LD, DD+WL, WD, WW): {string.Join(", ", Pentanomial)}");
        if (pairs.Count > 0)
        {
            var elo = EloEstimate.FromPentanomial(Pentanomial);
            text.AppendLine($"Elo: {elo}");
        }

        foreach (var ((player, forfeit), count) in forfeits.OrderBy(entry => entry.Key.Player))
            text.AppendLine($"Losses by {GameRecord.Describe(forfeit)} for {player}: {count}");

        return text.ToString();
    }
}
