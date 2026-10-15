using ChessLab.Arena.Statistics;

namespace ChessLab.Engine.Tests.Statistics;

public class SprtTests
{
    public static TheoryData<double, double, long[], double> FishtestRuns => new()
    {
        { 0, 2, [106, 10068, 27332, 10429, 113], 2.985190799167497 },
        { 0, 2, [130, 13936, 31332, 13814, 148], -2.9710440300705914 },
        { -1.75, 0.25, [318, 35823, 80249, 35081, 401], -2.9531708651198074 },
        { 0, 2, [40, 3706, 8126, 3496, 40], -2.9571676632833945 },
        { 0, 2, [19, 1575, 4199, 1633, 14], 0.33664685214901113 },
    };

    [Theory]
    [MemberData(nameof(FishtestRuns))]
    public void NormalizedLlr_MatchesPublishedFishtestRuns(double elo0, double elo1, long[] pentanomial, double published)
    {
        var llr = new Sprt(elo0, elo1).Llr(pentanomial);

        Assert.Equal(published, llr, 6);
    }

    [Fact]
    public void Bounds_FollowAlphaAndBeta()
    {
        var sprt = new Sprt(0, 5);

        Assert.Equal(Math.Log(0.05 / 0.95), sprt.LowerBound, 12);
        Assert.Equal(Math.Log(0.95 / 0.05), sprt.UpperBound, 12);
    }

    [Fact]
    public void ClearImprovement_AcceptsH1Early()
    {
        var sprt = new Sprt(0, 5);

        Assert.Equal(SprtOutcome.H1Accepted, sprt.Decide([6, 60, 180, 150, 90]));
    }

    [Fact]
    public void ClearRegression_AcceptsH0()
    {
        var sprt = new Sprt(0, 5);

        Assert.Equal(SprtOutcome.H0Accepted, sprt.Decide([90, 150, 180, 60, 6]));
    }

    [Fact]
    public void UndecidedAtTheGameLimit_IsInconclusive()
    {
        var sprt = new Sprt(0, 5, GameLimit: 200);

        Assert.Equal(SprtOutcome.Inconclusive, sprt.Decide([5, 20, 50, 20, 5]));
    }

    [Fact]
    public void UndecidedBelowTheGameLimit_Continues()
    {
        var sprt = new Sprt(0, 5);

        Assert.Equal(SprtOutcome.Continue, sprt.Decide([5, 20, 50, 20, 5]));
    }

    [Fact]
    public void EloEstimate_IsZeroForASymmetricResult()
    {
        var estimate = EloEstimate.FromPentanomial([10, 20, 40, 20, 10]);

        Assert.Equal(0, estimate.Elo, 9);
        Assert.True(estimate.Low < 0 && estimate.High > 0);
    }

    [Fact]
    public void EloEstimate_MatchesTheLogisticFormula()
    {
        var estimate = EloEstimate.FromPentanomial([1, 2, 10, 4, 3]);

        Assert.Equal(EloEstimate.Logistic(((0.5 * 2) + 10 + (1.5 * 4) + (2 * 3)) / 40.0), estimate.Elo, 9);
    }

    [Fact]
    public void Report_StatesOutcomeGamesAndInterval()
    {
        var sprt = new Sprt(0, 5);
        long[] pentanomial = [6, 60, 180, 150, 90];

        var report = sprt.Report(pentanomial, sprt.Decide(pentanomial));

        Assert.Contains("Outcome: H1 accepted", report);
        Assert.Contains("Games: 972", report);
        Assert.Contains("95% interval", report);
    }
}
