using System.Globalization;
using System.Text;

namespace ChessLab.Arena.Statistics;

public enum EloModel
{
    Normalized,
    Logistic,
}

public enum SprtOutcome
{
    Continue,
    H0Accepted,
    H1Accepted,
    Inconclusive,
}

public sealed record Sprt(double Elo0, double Elo1, double Alpha = 0.05, double Beta = 0.05,
    EloModel Model = EloModel.Normalized, int GameLimit = 40000)
{
    public double LowerBound => Math.Log(Beta / (1 - Alpha));

    public double UpperBound => Math.Log((1 - Beta) / Alpha);

    public double Llr(IReadOnlyList<long> pentanomial) => Model == EloModel.Normalized
        ? LikelihoodRatio.Normalized(Elo0, Elo1, pentanomial)
        : LikelihoodRatio.Logistic(Elo0, Elo1, pentanomial);

    public SprtOutcome Decide(IReadOnlyList<long> pentanomial)
    {
        var games = 2 * pentanomial.Sum();
        if (games == 0)
            return SprtOutcome.Continue;

        var llr = Llr(pentanomial);
        if (llr >= UpperBound)
            return SprtOutcome.H1Accepted;
        if (llr <= LowerBound)
            return SprtOutcome.H0Accepted;

        return games >= GameLimit ? SprtOutcome.Inconclusive : SprtOutcome.Continue;
    }

    public string Report(IReadOnlyList<long> pentanomial, SprtOutcome outcome)
    {
        var text = new StringBuilder();
        var invariant = CultureInfo.InvariantCulture;
        text.AppendLine(string.Create(invariant, $"Outcome: {Describe(outcome)}"));
        text.AppendLine(string.Create(invariant, $"Games: {2 * pentanomial.Sum()}"));
        text.AppendLine(string.Create(invariant, $"Elo: {EloEstimate.FromPentanomial(pentanomial)}"));
        text.AppendLine(string.Create(invariant,
            $"LLR: {Llr(pentanomial):0.00} ({LowerBound:0.00}, {UpperBound:0.00}) [{Elo0:0.##}, {Elo1:0.##}] {Model.ToString().ToLowerInvariant()} Elo, alpha {Alpha}, beta {Beta}"));
        text.AppendLine(string.Create(invariant, $"Pentanomial: {string.Join(", ", pentanomial)}"));
        return text.ToString();
    }

    public static string Describe(SprtOutcome outcome) => outcome switch
    {
        SprtOutcome.H0Accepted => "H0 accepted",
        SprtOutcome.H1Accepted => "H1 accepted",
        SprtOutcome.Inconclusive => "inconclusive",
        _ => "running",
    };
}
