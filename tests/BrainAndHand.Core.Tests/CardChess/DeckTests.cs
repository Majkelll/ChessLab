using BrainAndHand.Core.CardChess;

namespace BrainAndHand.Core.Tests.CardChess;

public class DeckTests
{
    [Fact]
    public void AllRanks_Has13DistinctValues()
    {
        Assert.Equal(13, Deck.AllRanks.Count);
        Assert.Equal(13, Deck.AllRanks.Distinct().Count());
    }

    [Fact]
    public void Draw_ReturnsCardsInOrder()
    {
        var deck = new Deck([CardRank.King, CardRank.Ace, CardRank.Two]);

        Assert.Equal(CardRank.King, deck.Draw());
        Assert.Equal(CardRank.Ace, deck.Draw());
        Assert.Equal(CardRank.Two, deck.Draw());
    }

    [Fact]
    public void Draw_MovesCardsFromDrawPileToDiscardPile()
    {
        var deck = new Deck([CardRank.King, CardRank.Ace]);

        deck.Draw();

        Assert.Equal(1, deck.DrawPileCount);
        Assert.Equal(1, deck.DiscardPileCount);
    }

    [Fact]
    public void Draw_WhenDrawPileEmpty_ReshufflesDiscardPileBackIn()
    {
        var deck = new Deck([CardRank.King, CardRank.Ace], new Random(1));
        deck.Draw();
        deck.Draw();
        Assert.Equal(0, deck.DrawPileCount);

        var third = deck.Draw();

        Assert.True(third is CardRank.King or CardRank.Ace);
        Assert.Equal(1, deck.DrawPileCount);
        Assert.Equal(1, deck.DiscardPileCount);
    }

    [Fact]
    public void Draw_WhenBothPilesEmpty_Throws()
    {
        var deck = new Deck([]);

        Assert.Throws<InvalidOperationException>(() => deck.Draw());
    }

    [Fact]
    public void Shuffled_ContainsExactlyOneOfEachRank()
    {
        var deck = Deck.Shuffled(new Random(42));

        var drawn = Enumerable.Range(0, Deck.AllRanks.Count).Select(_ => deck.Draw()).ToArray();

        Assert.Equal(Deck.AllRanks.OrderBy(r => r), drawn.OrderBy(r => r));
    }
}
