namespace ChessLab.Arena.Statistics;

public static class Pentanomial
{
    private const double RegularizationEpsilon = 1e-3;

    public static double[] Regularize(IReadOnlyList<long> results) =>
        [.. results.Select(frequency => frequency == 0 ? RegularizationEpsilon : frequency)];

    public static (double Count, Distribution Pdf) ToDistribution(IReadOnlyList<long> results)
    {
        var regularized = Regularize(results);
        var total = regularized.Sum();
        var points = regularized.Select((frequency, i) => (Value: i / (double)(regularized.Length - 1), Probability: frequency / total));
        return (total, new Distribution([.. points]));
    }
}

public sealed record Distribution(IReadOnlyList<(double Value, double Probability)> Points)
{
    public (double Mean, double Variance) Stats()
    {
        var mean = Points.Sum(point => point.Probability * point.Value);
        var variance = Points.Sum(point => point.Probability * Math.Pow(point.Value - mean, 2));
        return (mean, variance);
    }
}
