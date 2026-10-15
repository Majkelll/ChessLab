using System.Diagnostics;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Matches;

public static class GameRunner
{
    public const int ActionLimit = 3000;

    private static readonly TimeSpan CancellationGrace = TimeSpan.FromSeconds(1);

    public static async Task<GameRecord> PlayAsync(GameKind kind, PositionLine opening, int seed,
        IPlayerFactory white, IPlayerFactory black, CancellationToken ct)
    {
        var session = CoreGames.Start(kind, opening, seed);
        await using var whitePlayer = await white.CreateAsync(kind, unchecked((seed * 2) + 1), ct);
        await using var blackPlayer = await black.CreateAsync(kind, unchecked((seed * 2) + 2), ct);

        var actions = 0;
        while (session.HasActiveGame)
        {
            foreach (var seat in session.ActiveSeats.ToArray())
            {
                if (!session.HasActiveGame || !session.ActiveSeats.Contains(seat))
                    continue;

                if (++actions > ActionLimit)
                    return new GameRecord(null, "action limit", null, actions, session.Game!.PositionText);

                var (factory, player) = seat.Side == Side.White ? (white, whitePlayer) : (black, blackPlayer);
                var forfeit = await PlaySeatAsync(session, seat, factory, player, ct);
                if (forfeit is { } reason)
                    return new GameRecord(seat.Side.Opposite(), GameRecord.Describe(reason), reason, actions,
                        session.Game!.PositionText);
            }
        }

        var game = session.Game!;
        return new GameRecord(game.EndResult?.Winner, game.EndResult?.Reason.ToString() ?? "unknown", null, actions,
            game.PositionText);
    }

    private static async Task<Forfeit?> PlaySeatAsync(IRoomSession session, SeatId seat, IPlayerFactory factory,
        IArenaPlayer player, CancellationToken ct)
    {
        var budget = factory.BudgetFor(session.Room.Kind);
        var request = new BotActionRequest(session.Room.Kind, seat, factory.Difficulty, session.ToStateDto(seat.Side));

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter((2 * budget) + CancellationGrace);

        GameAction action;
        var clock = Stopwatch.StartNew();
        try
        {
            action = await player.ChooseAsync(request, limit.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Forfeit.TimeOverrun;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return Forfeit.Crash;
        }

        if (clock.Elapsed > 2 * budget)
            return Forfeit.TimeOverrun;

        try
        {
            CoreGames.Apply(session, new SeatAction(seat, action));
            return null;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return Forfeit.IllegalAction;
        }
    }
}
