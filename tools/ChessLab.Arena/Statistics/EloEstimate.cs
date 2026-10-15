using System.Globalization;

namespace ChessLab.Arena.Statistics;

public readonly record struct EloEstimate(double Elo, double Low, double High, double ErrorMargin)
{
    private const double Z975 = 1.959963984540054;

    public static EloEstimate FromPentanomial(IReadOnlyList<long> pentanomial) => FromFrequencies(pentanomial);

    public static EloEstimate FromFrequencies(IReadOnlyList<long> frequencies)
    {
        var results = Pentanomial.Regularize(frequencies);
        var count = results.Length;
        var total = results.Sum();
        var games = total * (count - 1) / 2.0;

        var mu = results.Select((frequency, i) => frequency * (i / 2.0)).Sum() / games;
        var muPerSample = (count - 1) / 2.0 * mu;
        var variance = results.Select((frequency, i) => frequency * Math.Pow((i / 2.0) - muPerSample, 2)).Sum() / games;
        var stdev = Math.Sqrt(variance);

        var muMin = mu - (Z975 * stdev / Math.Sqrt(games));
        var muMax = mu + (Z975 * stdev / Math.Sqrt(games));
        var low = Logistic(muMin);
        var high = Logistic(muMax);
        return new EloEstimate(Logistic(mu), low, high, (high - low) / 2);
    }

    public static double Logistic(double score)
    {
        score = Math.Clamp(score, 1e-3, 1 - 1e-3);
        return -400 * Math.Log10((1 / score) - 1);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{Elo:+0.0;-0.0;0.0} ± {ErrorMargin:0.0} (95% interval {Low:0.0} to {High:0.0})");
}
