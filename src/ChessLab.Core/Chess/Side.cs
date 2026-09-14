namespace ChessLab.Core.Chess;

public enum Side
{
    White,
    Black,
}

public static class SideExtensions
{
    public static Side Opposite(this Side side) => side == Side.White ? Side.Black : Side.White;
}
