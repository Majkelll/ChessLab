using ChessLab.Core.Chess;
using ChessLab.Core.CardChess;

namespace ChessLab.Core.Tests.CardChess;

public class CardRankTests
{
    [Theory]
    [InlineData(CardRank.Two, PieceKind.Pawn)]
    [InlineData(CardRank.Nine, PieceKind.Pawn)]
    [InlineData(CardRank.Ten, PieceKind.Knight)]
    [InlineData(CardRank.Jack, PieceKind.Bishop)]
    [InlineData(CardRank.Queen, PieceKind.Queen)]
    [InlineData(CardRank.King, PieceKind.King)]
    [InlineData(CardRank.Ace, PieceKind.Rook)]
    public void ToPieceKind_MapsEachRankToItsPiece(CardRank card, PieceKind expectedKind)
    {
        Assert.Equal(expectedKind, card.ToPieceKind());
    }

    [Fact]
    public void Label_MatchesTheRulesTableExactly()
    {
        Assert.Equal("2", CardRank.Two.Label());
        Assert.Equal("9", CardRank.Nine.Label());
        Assert.Equal("10", CardRank.Ten.Label());
        Assert.Equal("J", CardRank.Jack.Label());
        Assert.Equal("Q", CardRank.Queen.Label());
        Assert.Equal("K", CardRank.King.Label());
        Assert.Equal("A", CardRank.Ace.Label());
    }
}
