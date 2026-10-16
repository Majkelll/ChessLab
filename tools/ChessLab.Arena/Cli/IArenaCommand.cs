namespace ChessLab.Arena.Cli;

public interface IArenaCommand
{
    string Name { get; }

    string Summary { get; }

    string Usage { get; }

    Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct);
}
