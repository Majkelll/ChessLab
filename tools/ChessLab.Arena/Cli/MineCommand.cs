using System.Globalization;
using ChessLab.Arena.Mining;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Cli;

public sealed class MineCommand : IArenaCommand
{
    public string Name => "mine";

    public string Summary => "Find bot blunders in finished full-information games and add them to review lists.";

    public string Usage =>
        "mine --connection <connection string> [--days 1 | --since yyyy-mm-dd] [--out ~/chesslab-arena/review] [--data <dir>]\n" +
        "  a connection string starting with 'Data Source=' is read as SQLite, anything else as PostgreSQL";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var data = CommonOptions.Data(args);
        var outDirectory = args.Optional("out") ?? Path.Combine(Anchors.DefaultRoot, "review");
        var since = args.Optional("since") is { } sinceText
            ? DateTime.SpecifyKind(DateTime.Parse(sinceText, CultureInfo.InvariantCulture), DateTimeKind.Utc)
            : DateTime.UtcNow.AddDays(-args.Int("days", 1));

        await using var db = GameRecords.Open(args.Required("connection"));
        var games = await GameRecords.FinishedBotGamesAsync(db, since, ct);
        var found = new Dictionary<GameKind, List<string>>();

        foreach (var game in games)
        {
            ct.ThrowIfCancellationRequested();
            var settingsPath = data.FileOf(game.Kind, "mining.json");
            if (!File.Exists(settingsPath))
                continue;

            IReadOnlyList<ReplayedTurn> turns;
            try
            {
                turns = GameReplay.Replay(game.Kind, game.MovesSan.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
            catch (InvalidOperationException ex)
            {
                output.WriteLine($"{game.RoomCode}: skipped, {ex.Message}");
                continue;
            }

            var botSides = game.Players.Where(player => player.BotDifficulty is not null)
                .Select(player => player.Side).ToHashSet();
            foreach (var blunder in BlunderMiner.Find(game.Kind, turns, botSides, MiningSettings.Load(settingsPath)))
            {
                output.WriteLine($"{game.RoomCode} {Modes.FolderOf(game.Kind)}: {blunder.Played} lost {blunder.Loss}, " +
                                 $"better {blunder.Better.Expected}");
                (found.TryGetValue(game.Kind, out var list) ? list : found[game.Kind] = []).Add(blunder.Better.ToString());
            }
        }

        foreach (var (kind, lines) in found)
        {
            var path = Path.Combine(outDirectory, Modes.FolderOf(kind), "review.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var existing = File.Exists(path) ? (await File.ReadAllLinesAsync(path, ct)).ToHashSet() : [];
            await File.AppendAllLinesAsync(path, lines.Where(existing.Add), ct);
        }

        output.WriteLine($"Mined {games.Count} games since {since:yyyy-MM-dd}, {found.Values.Sum(list => list.Count)} positions for review");
        return 0;
    }
}
