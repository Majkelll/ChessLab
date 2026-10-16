using ChessLab.Arena.Cli;

namespace ChessLab.Engine.Tests.Suites;

public class CliTests
{
    [Fact]
    public async Task Help_ListsEveryCommand()
    {
        var output = new StringWriter();

        var exitCode = await ArenaCli.RunAsync(["--help"], output, output);

        Assert.Equal(0, exitCode);
        foreach (var command in new[] { "match", "sprt", "calibrate", "bench", "bench-compare", "tactics", "perft", "fuzz",
                     "invariants", "anchor", "ladder", "mine", "openings", "serve-bot" })
            Assert.Contains($"  {command} ", output.ToString());
    }

    [Fact]
    public async Task UnknownCommandOrMode_IsReportedWithoutAStackTrace()
    {
        var output = new StringWriter();

        Assert.Equal(1, await ArenaCli.RunAsync(["castle"], output, output));
        Assert.Equal(2, await ArenaCli.RunAsync(["perft", "--mode", "fairy-chess"], output, output));
        Assert.Contains("Unknown mode 'fairy-chess'", output.ToString());
    }
}
