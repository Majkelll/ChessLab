namespace ChessLab.Arena.Cli;

public static class ArenaCli
{
    public static IReadOnlyList<IArenaCommand> Commands { get; } =
    [
        new MatchCommand(),
        new SprtCommand(),
        new CalibrateCommand(),
        new BenchCommand(),
        new BenchCompareCommand(),
        new TacticsCommand(),
        new PerftCommand(),
        new FuzzCommand(),
        new InvariantsCommand(),
        new AnchorCommand(),
        new LadderCommand(),
        new MineCommand(),
        new OpeningsCommand(),
        new ServeBotCommand(),
    ];

    public static async Task<int> RunAsync(string[] arguments, TextWriter output, TextWriter errors,
        CancellationToken ct = default)
    {
        if (arguments.Length == 0 || arguments[0] is "--help" or "-h" or "help")
        {
            WriteHelp(output);
            return arguments.Length == 0 ? 1 : 0;
        }

        var command = Commands.FirstOrDefault(candidate => candidate.Name == arguments[0]);
        if (command is null)
        {
            errors.WriteLine($"Unknown command '{arguments[0]}'.");
            WriteHelp(errors);
            return 1;
        }

        var args = new CommandArgs(arguments.Skip(1));
        if (args.Has("help"))
        {
            output.WriteLine($"Usage: arena {command.Usage}");
            return 0;
        }

        try
        {
            return await command.RunAsync(args, output, ct);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or FileNotFoundException
                                       or DirectoryNotFoundException)
        {
            errors.WriteLine($"arena {command.Name}: {ex.Message}");
            return 2;
        }
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("ChessLab arena: measures bot engines with ChessLab.Core as the referee.");
        output.WriteLine();
        output.WriteLine("Commands:");
        foreach (var command in Commands)
            output.WriteLine($"  {command.Name,-14} {command.Summary}");

        output.WriteLine();
        output.WriteLine("Run 'arena <command> --help' for the options of a command.");
    }
}
