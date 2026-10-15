using ChessLab.Arena.Cli;

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

return await ArenaCli.RunAsync(args, Console.Out, Console.Error, cancellation.Token);
