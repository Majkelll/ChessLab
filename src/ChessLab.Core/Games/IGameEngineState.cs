using ChessLab.Core.Chess;

namespace ChessLab.Core.Games;

public interface IGameEngineState
{
    Side SideToMove { get; }
    Clock Clock { get; }
    GameEndResult? EndResult { get; }
    bool IsGameOver { get; }
    IReadOnlyList<string> MoveNotations { get; }
    string PositionText { get; }

    IReadOnlyList<Side> SidesOnTheClock { get; }

    void Resign(Side side);
    void DeclareTimeoutIfFlagged();
}
