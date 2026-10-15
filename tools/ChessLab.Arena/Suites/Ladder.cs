using System.Globalization;
using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Statistics;

namespace ChessLab.Arena.Suites;

public static class Ladder
{
    public const string Header = "date,mode,commit,player,opponent,games,score,elo,elo_low,elo_high";

    public static string Row(DateTimeOffset date, string commit, MatchReport match)
    {
        var elo = EloEstimate.FromPentanomial(match.Pentanomial);
        return string.Create(CultureInfo.InvariantCulture,
            $"{date:yyyy-MM-dd},{Modes.FolderOf(match.Kind)},{commit},{match.Candidate},{match.Baseline},{match.Games}," +
            $"{match.Score:0.0000},{elo.Elo:0.0},{elo.Low:0.0},{elo.High:0.0}");
    }

    public static async Task AppendAsync(string path, IEnumerable<string> rows, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        if (!File.Exists(path))
            await File.WriteAllTextAsync(path, Header + Environment.NewLine, ct);

        await File.AppendAllLinesAsync(path, rows, ct);
    }
}
