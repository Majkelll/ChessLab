using BrainAndHand.Core.Chess;

namespace BrainAndHand.Core.Rooms;

/// <summary>A lobby of 4 seats (White/Black x Brain/Hand) identified by an invite code.</summary>
public sealed class Room
{
    public static IReadOnlyList<SeatId> AllSeatIds { get; } =
    [
        new(Side.White, SeatRole.Brain),
        new(Side.White, SeatRole.Hand),
        new(Side.Black, SeatRole.Brain),
        new(Side.Black, SeatRole.Hand),
    ];

    private readonly Dictionary<SeatId, SeatOccupant> seats =
        AllSeatIds.ToDictionary(id => id, _ => SeatOccupant.Empty);

    public string Code { get; }
    public Guid HostUserId { get; }
    public bool IsLocked { get; private set; }

    public Room(string code, Guid hostUserId)
    {
        Code = code;
        HostUserId = hostUserId;
    }

    public IReadOnlyDictionary<SeatId, SeatOccupant> Seats => seats;

    public bool IsFull => seats.Values.All(o => o.Kind != OccupantKind.Empty);

    public void ClaimSeat(SeatId seatId, Guid userId, string displayName)
    {
        EnsureNotLocked();

        if (seats[seatId].Kind != OccupantKind.Empty)
            throw new InvalidOperationException("Seat is already taken.");

        // A player occupies at most one seat at a time.
        foreach (var (id, occupant) in seats)
        {
            if (occupant.Kind == OccupantKind.Human && occupant.UserId == userId)
                seats[id] = SeatOccupant.Empty;
        }

        seats[seatId] = SeatOccupant.Human(userId, displayName);
    }

    public void LeaveSeat(SeatId seatId, Guid userId)
    {
        EnsureNotLocked();

        if (seats[seatId].Kind == OccupantKind.Human && seats[seatId].UserId == userId)
            seats[seatId] = SeatOccupant.Empty;
    }

    public void SetBot(SeatId seatId, BotDifficulty difficulty)
    {
        EnsureNotLocked();

        if (seats[seatId].Kind == OccupantKind.Human)
            throw new InvalidOperationException("Seat is occupied by a human player.");

        seats[seatId] = SeatOccupant.Bot(difficulty);
    }

    public void ClearSeat(SeatId seatId)
    {
        EnsureNotLocked();
        seats[seatId] = SeatOccupant.Empty;
    }

    public SeatId? FindSeatOf(Guid userId)
    {
        foreach (var (id, occupant) in seats)
        {
            if (occupant.Kind == OccupantKind.Human && occupant.UserId == userId)
                return id;
        }

        return null;
    }

    internal void Lock()
    {
        if (!IsFull)
            throw new InvalidOperationException("All seats must be filled before the room can be locked.");

        IsLocked = true;
    }

    private void EnsureNotLocked()
    {
        if (IsLocked)
            throw new InvalidOperationException("The room is locked because the game has already started.");
    }
}
