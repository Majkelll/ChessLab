namespace ChessLab.Core.CardChess;

public sealed class Deck
{
    public static readonly IReadOnlyList<CardRank> AllRanks = Enum.GetValues<CardRank>();

    private readonly List<CardRank> drawPile;
    private readonly List<CardRank> discardPile = [];
    private readonly Random random;

    public Deck(IReadOnlyList<CardRank> order, Random? random = null)
    {
        drawPile = [.. order];
        this.random = random ?? Random.Shared;
    }

    public static Deck Shuffled(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        var order = AllRanks.ToList();
        Shuffle(order, rng);
        return new Deck(order, rng);
    }

    public int DrawPileCount => drawPile.Count;
    public int DiscardPileCount => discardPile.Count;

    public CardRank Draw()
    {
        if (drawPile.Count == 0)
        {
            if (discardPile.Count == 0)
                throw new InvalidOperationException("No cards left to draw.");

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            Shuffle(drawPile, random);
        }

        var card = drawPile[0];
        drawPile.RemoveAt(0);
        discardPile.Add(card);
        return card;
    }

    public CardRank Draw(params Func<CardRank, bool>[] preferences)
    {
        foreach (var isWanted in preferences)
        {
            for (var attempt = 0; attempt < AllRanks.Count; attempt++)
            {
                var card = Draw();
                if (isWanted(card))
                    return card;
            }
        }

        return Draw();
    }

    private static void Shuffle(List<CardRank> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
