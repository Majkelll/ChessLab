namespace ChessLab.Core.Rooms;

/// <summary>Which game a room is for — determines its seat shape and which game-state type it holds.</summary>
public enum GameKind
{
    HandAndBrain,
    CardChess,
    ArcaneChess,
    BiddingChess,
}
