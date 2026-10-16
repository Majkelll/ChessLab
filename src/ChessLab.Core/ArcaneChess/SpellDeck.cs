using ChessLab.Core.Games;

namespace ChessLab.Core.ArcaneChess;

public sealed class SpellDeck
{
    private readonly List<SpellRank> drawPile;
    private readonly List<SpellRank> discardPile = [];
    private GameRandom random;

    public SpellDeck(IReadOnlyList<SpellRank> order, GameRandom? random = null)
    {
        drawPile = [.. order];
        this.random = random ?? GameRandom.Unseeded();
    }

    public void Reseed(GameRandom newRandom)
    {
        random = newRandom;
        drawPile.AddRange(discardPile);
        discardPile.Clear();
        Shuffle(drawPile, random);
    }

    public SpellDeck Clone()
    {
        var copy = new SpellDeck(drawPile, random.Clone());
        copy.discardPile.AddRange(discardPile);
        return copy;
    }

    public static SpellDeck Shuffled(GameRandom? random = null)
    {
        var rng = random ?? GameRandom.Unseeded();
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

    private static void Shuffle(List<SpellRank> list, GameRandom rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
