namespace ChessLab.Core.ArcaneChess;

/// <summary>One copy of each <see cref="SpellRank"/>, drawn and reshuffled the same way as Card
/// Chess's rank <see cref="ChessLab.Core.CardChess.Deck"/> — kept as a separate small type rather
/// than sharing that one, since spells don't need Card Chess's "keep drawing until playable" logic.</summary>
public sealed class SpellDeck
{
    private readonly List<SpellRank> drawPile;
    private readonly List<SpellRank> discardPile = [];
    private readonly Random random;

    public SpellDeck(IReadOnlyList<SpellRank> order, Random? random = null)
    {
        drawPile = [.. order];
        this.random = random ?? Random.Shared;
    }

    public static SpellDeck Shuffled(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        var order = SpellRankExtensions.All.ToList();
        Shuffle(order, rng);
        return new SpellDeck(order, rng);
    }

    public SpellRank Draw()
    {
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0)
                throw new InvalidOperationException("No spell cards left to draw.");

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile, random);
        }

        var card = drawPile[0];
        drawPile.RemoveAt(0);
        discardPile.Add(card);
        return card;
    }

    private static void Shuffle(List<SpellRank> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
