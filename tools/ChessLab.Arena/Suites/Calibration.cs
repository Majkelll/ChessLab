using System.Globalization;
using ChessLab.Arena.Matches;
using ChessLab.Arena.Players;
using ChessLab.Arena.Statistics;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Suites;

public sealed record CalibrationGap(BotDifficulty Lower, BotDifficulty Higher, MatchReport Match)
{
    public bool Passed => Match.Score > 0.5;

    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"{Higher} against {Lower}: score {Match.Score:P1} over {Match.Games} games, Elo {EloEstimate.FromPentanomial(Match.Pentanomial)}" +
        $"{(Passed ? "" : " CALIBRATION FAILURE: not stronger than the level below")}");
}

public static class Calibration
{
    public static async Task<IReadOnlyList<CalibrationGap>> MeasureAsync(GameKind kind, ArenaSettings settings,
        IReadOnlyList<Notation.PositionLine> openings, int pairs, int concurrency, CancellationToken ct)
    {
        var levels = Enum.GetValues<BotDifficulty>();
        var gaps = new List<CalibrationGap>();

        for (var i = 1; i < levels.Length; i++)
        {
            var match = await MatchRunner.RunAsync(new MatchSettings(kind,
                new BotPlayerFactory(levels[i], settings), new BotPlayerFactory(levels[i - 1], settings),
                openings, pairs, concurrency), ct: ct);
            gaps.Add(new CalibrationGap(levels[i - 1], levels[i], match));
        }

        return gaps;
    }
}
