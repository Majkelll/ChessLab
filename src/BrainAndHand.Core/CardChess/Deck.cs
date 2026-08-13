namespace BrainAndHand.Core.CardChess;

/// <summary>A player's personal 13-card deck (one of each rank — suit doesn't matter, since the
/// card→piece mapping only cares about rank). Draws come off the top; once the draw pile is empty
/// the discard pile is reshuffled back in, so a deck never truly runs out mid-game.</summary>
public sealed class Deck
{
    public static readonly IReadOnlyList<CardRank> AllRanks = Enum.GetValues<CardRank>();

    private readonly List<CardRank> drawPile;
    private readonly List<CardRank> discardPile = [];
    private readonly Random random;

    /// <summary>Cards come off the front of <paramref name="order"/> first — pass an explicit order
    /// for deterministic tests, or use <see cref="Shuffled"/> for real games. <paramref name="random"/>
    /// (defaulting to <see cref="Random.Shared"/>) is only consulted later, to reshuffle the discard
    /// pile once <paramref name="order"/> is exhausted.</summary>
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

    private static void Shuffle(List<CardRank> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
