using ChessLab.Arena.Cli;

namespace ChessLab.Engine.Tests.Suites;

public class BenchCompareTests
{
    [Theory]
    [InlineData(800, 1, "too slow")]
    [InlineData(950, 0, "-5.0%")]
    [InlineData(1100, 0, "+10.0%")]
    public async Task NodesPerSecondBelowMain_FailOnlyBeyondTenPercent(int candidateNps, int exitCode, string expected)
    {
        var directory = Directory.CreateTempSubdirectory("bench-compare");
        try
        {
            var main = Path.Combine(directory.FullName, "main.txt");
            var candidate = Path.Combine(directory.FullName, "candidate.txt");
            await File.WriteAllTextAsync(main, "martian-chess 4944 1000\n");
            await File.WriteAllTextAsync(candidate, $"martian-chess 4944 {candidateNps}\n");

            var output = new StringWriter();
            var result = await ArenaCli.RunAsync(["bench-compare", "--main", main, "--candidate", candidate], output, output);

            Assert.Equal(exitCode, result);
            Assert.Contains(expected, output.ToString());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ChangedNodeCount_IsReported()
    {
        var directory = Directory.CreateTempSubdirectory("bench-compare");
        try
        {
            var main = Path.Combine(directory.FullName, "main.txt");
            var candidate = Path.Combine(directory.FullName, "candidate.txt");
            await File.WriteAllTextAsync(main, "martian-chess 4944 1000\n");
            await File.WriteAllTextAsync(candidate, "martian-chess 5000 1000\n");

            var output = new StringWriter();
            await ArenaCli.RunAsync(["bench-compare", "--main", main, "--candidate", candidate], output, output);

            Assert.Contains("bench changed 4944 -> 5000", output.ToString());
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
