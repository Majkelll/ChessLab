using ChessLab.Core.Chess;

namespace ChessLab.Core.Games;

/// <summary>
/// The part of a game's state that every mode shares, whatever its rules are: whose turn it is,
/// the clock, whether it's over and how, plus a textual record of the position and the moves so
/// far. Position and move notation are strings rather than FEN/<see cref="ChessMove"/> because
/// modes like Martian Chess aren't played on an 8x8 chess board at all — the archive and the
/// history pages only ever need something printable.
/// </summary>
public interface IGameEngineState
{
    Side SideToMove { get; }
    Clock Clock { get; }
    GameEndResult? EndResult { get; }
    bool IsGameOver { get; }
    IReadOnlyList<string> MoveNotations { get; }
    string PositionText { get; }

    void Resign(Side side);
    void DeclareTimeoutIfFlagged();
}
