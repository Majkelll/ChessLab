using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Refereeing;

public static class CoreGames
{
    public static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan UnlimitedClock = TimeSpan.FromDays(30);

    public static IRoomSession Start(GameKind kind, PositionLine position, int? pairSeed = null)
    {
        var room = new Room("ARENA", Guid.Empty, kind);
        foreach (var seat in room.SeatIds)
            room.SetBot(seat, BotDifficulty.Easy);

        var session = RoomSessions.For(room);
        session.Start(UnlimitedClock, TimeSpan.Zero, Epoch, position.Seed);

        foreach (var token in position.Actions)
        {
            try
            {
                Apply(session, ActionNotation.Parse(token, kind));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FormatException)
            {
                throw new InvalidOperationException(
                    $"{Modes.FolderOf(kind)} position '{position}' cannot be replayed at '{token}': {ex.Message}", ex);
            }
        }

        if (pairSeed is { } seed)
            Reseed(session, seed);

        return session;
    }

    public static void Apply(IRoomSession session, SeatAction seatAction) =>
        session.Apply(seatAction.Action, seatAction.Seat, Epoch);

    public static IReadOnlyList<SeatAction> LegalActions(IRoomSession session) =>
        session.HasActiveGame
            ? [.. session.ActiveSeats.SelectMany(seat => SeatActions.For(session.ToStateDto(seat.Side), seat))]
            : [];

    public static void Reseed(IRoomSession session, int seed)
    {
        switch (session.Game)
        {
            case Core.CardChess.GameState card:
                card.Reseed(seed);
                break;
            case Core.ArcaneChess.GameState arcane:
                arcane.Reseed(seed);
                break;
        }
    }
}
