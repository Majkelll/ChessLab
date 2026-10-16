using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Suites;

public sealed record TacticPosition(string Expected, PositionLine Position)
{
    public static TacticPosition Parse(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            throw new FormatException($"'{line}' is not a tactic: expected the action, then the position.");

        return new TacticPosition(tokens[0], PositionLine.Parse(tokens[1..]));
    }

    public override string ToString() => $"{Expected} {Position}";
}

public interface ITacticSolver
{
    string Name { get; }

    Task<string?> SolveAsync(GameKind kind, TacticPosition tactic, CancellationToken ct);
}

public sealed class PlayerSolver(IPlayerFactory factory) : ITacticSolver
{
    public string Name => factory.Name;

    public async Task<string?> SolveAsync(GameKind kind, TacticPosition tactic, CancellationToken ct)
    {
        var session = CoreGames.Start(kind, tactic.Position);
        var expected = ActionNotation.Parse(tactic.Expected, kind);
        await using var player = await factory.CreateAsync(kind, 1, ct);

        for (var attempt = 0; attempt < MaxActionsBeforeTheExpectedKind; attempt++)
        {
            var action = await ChooseAsync(kind, session, expected.Seat, player, ct);
            if (action is null || action.Kind == expected.Action.Kind)
                return action is null ? null : ActionNotation.Format(expected.Seat.Side, action);

            try
            {
                CoreGames.Apply(session, new SeatAction(expected.Seat, action));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return ActionNotation.Format(expected.Seat.Side, action);
            }
        }

        return null;
    }

    private const int MaxActionsBeforeTheExpectedKind = 4;

    private async Task<GameAction?> ChooseAsync(GameKind kind, IRoomSession session, SeatId seat, IArenaPlayer player,
        CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(2 * factory.BudgetFor(kind));
        try
        {
            var request = new BotActionRequest(kind, seat, factory.Difficulty, session.ToStateDto(seat.Side));
            return await player.ChooseAsync(request, limit.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}

public sealed class EngineSolver(int depth) : ITacticSolver
{
    public string Name => $"engine depth {depth}";

    public Task<string?> SolveAsync(GameKind kind, TacticPosition tactic, CancellationToken ct)
    {
        var engine = EngineRegistry.For(kind);
        engine.Load(tactic.Position);
        return Task.FromResult(engine.Search(depth).BestAction);
    }
}

public sealed record TacticsReport(string Solver, IReadOnlyList<TacticPosition> Solved, IReadOnlyList<(TacticPosition Tactic, string? Played)> Unsolved)
{
    public int Total => Solved.Count + Unsolved.Count;
}

public static class Tactics
{
    public static async Task<TacticsReport> SolveAsync(GameKind kind, IReadOnlyList<TacticPosition> suite,
        ITacticSolver solver, CancellationToken ct)
    {
        var solved = new List<TacticPosition>();
        var unsolved = new List<(TacticPosition, string?)>();

        foreach (var tactic in suite)
        {
            var played = await solver.SolveAsync(kind, tactic, ct);
            if (played == tactic.Expected)
                solved.Add(tactic);
            else
                unsolved.Add((tactic, played));
        }

        return new TacticsReport(solver.Name, solved, unsolved);
    }
}
