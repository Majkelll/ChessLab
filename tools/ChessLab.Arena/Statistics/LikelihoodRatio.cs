namespace ChessLab.Arena.Statistics;

public static class LikelihoodRatio
{
    public static readonly double NormalizedEloPerT = 800 / Math.Log(10);

    public static double Normalized(double nelo0, double nelo1, IReadOnlyList<long> pentanomial)
    {
        var (t0, t1) = (nelo0 / NormalizedEloPerT * Math.Sqrt(2), nelo1 / NormalizedEloPerT * Math.Sqrt(2));
        var (count, pdf) = Pentanomial.ToDistribution(pentanomial);
        return count * Llr(pdf, MleTValue(pdf, 0.5, t0), MleTValue(pdf, 0.5, t1));
    }

    public static double Logistic(double elo0, double elo1, IReadOnlyList<long> pentanomial)
    {
        var (s0, s1) = (ScoreOf(elo0), ScoreOf(elo1));
        var (count, pdf) = Pentanomial.ToDistribution(pentanomial);
        return count * Llr(pdf, MleExpected(pdf, s0), MleExpected(pdf, s1));
    }

    private static double ScoreOf(double elo) => 1 / (1 + Math.Pow(10, -elo / 400));

    private static double Llr(Distribution observed, double[] probabilities0, double[] probabilities1) =>
        observed.Points.Select((point, i) =>
            point.Probability * (Math.Log(probabilities1[i]) - Math.Log(probabilities0[i]))).Sum();

    private static double[] MleExpected(Distribution observed, double score)
    {
        var shifted = observed.Points.Select(point => point.Value - score).ToArray();
        var x = Secular(shifted, [.. observed.Points.Select(point => point.Probability)]);
        return [.. observed.Points.Select((point, i) => point.Probability / (1 + (x * shifted[i])))];
    }

    private static double[] MleTValue(Distribution observed, double reference, double t)
    {
        var count = observed.Points.Count;
        var values = observed.Points.Select(point => point.Value).ToArray();
        var observedProbabilities = observed.Points.Select(point => point.Probability).ToArray();
        var probabilities = Enumerable.Repeat(1.0 / count, count).ToArray();

        for (var iteration = 0; iteration < 10; iteration++)
        {
            var previous = probabilities;
            var (mu, variance) = new Distribution([.. values.Zip(probabilities)]).Stats();
            var sigma = Math.Sqrt(variance);
            var shifted = values
                .Select(value => value - reference - (t * sigma * (1 + Math.Pow((mu - value) / sigma, 2)) / 2))
                .ToArray();

            var x = Secular(shifted, observedProbabilities);
            probabilities = [.. observedProbabilities.Select((probability, i) => probability / (1 + (x * shifted[i])))];

            if (probabilities.Zip(previous).Max(pair => Math.Abs(pair.First - pair.Second)) < 1e-9)
                break;
        }

        return probabilities;
    }

    private static double Secular(double[] values, double[] probabilities)
    {
        const double epsilon = 1e-9;
        var (lowest, highest) = (values.Min(), values.Max());
        if (lowest * highest >= 0)
            throw new ArgumentException("The results do not straddle the tested score.");

        double F(double x) => values.Select((value, i) => probabilities[i] * value / (1 + (x * value))).Sum();

        var low = (-1 / highest) + epsilon;
        var high = (-1 / lowest) - epsilon;
        for (var iteration = 0; iteration < 200 && high - low > 1e-15; iteration++)
        {
            var middle = (low + high) / 2;
            if (F(middle) > 0)
                low = middle;
            else
                high = middle;
        }

        return (low + high) / 2;
    }
}
