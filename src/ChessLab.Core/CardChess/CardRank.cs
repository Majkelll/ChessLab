using ChessLab.Core.Chess;

namespace ChessLab.Core.CardChess;

public enum CardRank
{
    Two,
    Three,
    Four,
    Five,
    Six,
    Seven,
    Eight,
    Nine,
    Ten,
    Jack,
    Queen,
    King,
    Ace,
}

public static class CardRankExtensions
{
    public static PieceKind ToPieceKind(this CardRank card) => card switch
    {
        >= CardRank.Two and <= CardRank.Nine => PieceKind.Pawn,
        CardRank.Ten => PieceKind.Knight,
        CardRank.Jack => PieceKind.Bishop,
        CardRank.Queen => PieceKind.Queen,
        CardRank.King => PieceKind.King,
        CardRank.Ace => PieceKind.Rook,
        _ => throw new ArgumentOutOfRangeException(nameof(card)),
    };

    public static string Label(this CardRank card) => card switch
    {
        CardRank.Two => "2",
        CardRank.Three => "3",
        CardRank.Four => "4",
        CardRank.Five => "5",
        CardRank.Six => "6",
        CardRank.Seven => "7",
        CardRank.Eight => "8",
        CardRank.Nine => "9",
        CardRank.Ten => "10",
        CardRank.Jack => "J",
        CardRank.Queen => "Q",
        CardRank.King => "K",
        CardRank.Ace => "A",
        _ => throw new ArgumentOutOfRangeException(nameof(card)),
    };
}
