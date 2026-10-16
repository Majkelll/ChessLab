using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Mining;

public sealed record ReplayedTurn(Side Actor, PositionLine Before, IReadOnlyList<string> Played);

public static class GameReplay
{
    private const int MaxActionsPerNotation = 2;

    public static IReadOnlyList<ReplayedTurn> Replay(GameKind kind, IReadOnlyList<string> notations)
    {
        var line = PositionLine.Start();
        var session = CoreGames.Start(kind, line);
        var turns = new List<ReplayedTurn>();

        foreach (var notation in notations)
        {
            var played = FindActions(session, notation, MaxActionsPerNotation)
                ?? throw new InvalidOperationException(
                    $"No {Modes.FolderOf(kind)} action in '{line}' produces the recorded move '{notation}'.");

            turns.Add(new ReplayedTurn(ActionNotation.Parse(played[0], kind).Seat.Side, line, played));
            foreach (var action in played)
            {
                CoreGames.Apply(session, ActionNotation.Parse(action, kind));
                line = line.Then(action);
            }
        }

        return turns;
    }

    private static IReadOnlyList<string>? FindActions(IRoomSession session, string notation, int depth)
    {
        var recorded = session.Game!.MoveNotations.Count;
        foreach (var action in CoreGames.LegalActions(session))
        {
            var next = session.Clone();
            CoreGames.Apply(next, action);
            var notations = next.Game!.MoveNotations;

            if (notations.Count > recorded)
            {
                if (notations[recorded] == notation)
                    return [action.ToString()];

                continue;
            }

            if (depth > 1 && FindActions(next, notation, depth - 1) is { } rest)
                return [action.ToString(), .. rest];
        }

        return null;
    }
}
