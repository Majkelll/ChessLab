using ChessLab.Core.Chess;

namespace ChessLab.Core.Rooms;

/// <summary>A lobby identified by an invite code. Seat shape depends on <see cref="Kind"/>: Hand &amp;
/// Brain uses 4 seats (White/Black x Brain/Hand), other game kinds may use fewer — see
/// <see cref="SeatIds"/>.</summary>
public sealed class Room
{
    /// <summary>The 4 Hand &amp; Brain seats — kept as the static default for backward compatibility
    /// (existing callers/tests that never pass a <see cref="GameKind"/> get this shape).</summary>
    public static IReadOnlyList<SeatId> AllSeatIds { get; } =
    [
        new(Side.White, SeatRole.Brain),
        new(Side.White, SeatRole.Hand),
        new(Side.Black, SeatRole.Brain),
        new(Side.Black, SeatRole.Hand),
    ];

    /// <summary>The 2 Card Chess / Arcane Chess seats — one per side, no role split.</summary>
    public static IReadOnlyList<SeatId> CardChessSeatIds { get; } =
    [
        new(Side.White, SeatRole.Player),
        new(Side.Black, SeatRole.Player),
    ];

    private static IReadOnlyList<SeatId> SeatIdsFor(GameKind kind) =>
        kind is GameKind.CardChess or GameKind.ArcaneChess ? CardChessSeatIds : AllSeatIds;

    private readonly Dictionary<SeatId, SeatOccupant> seats;

    public string Code { get; }
    public Guid HostUserId { get; }
    public GameKind Kind { get; }

    /// <summary>This room's seats, in a stable order — the shape depends on <see cref="Kind"/>.</summary>
    public IReadOnlyList<SeatId> SeatIds { get; }

    public bool IsLocked { get; private set; }

    /// <summary>Defaults to 10 minutes with no increment; the host can change this from the
    /// lobby (via <see cref="SetClockSettings"/>) any time before the game starts.</summary>
    public TimeSpan InitialClock { get; private set; }
    public TimeSpan ClockIncrement { get; private set; }

    public Room(string code, Guid hostUserId, GameKind kind = GameKind.HandAndBrain,
        TimeSpan? initialClock = null, TimeSpan? clockIncrement = null)
    {
        Code = code;
        HostUserId = hostUserId;
        Kind = kind;
        SeatIds = SeatIdsFor(kind);
        seats = SeatIds.ToDictionary(id => id, _ => SeatOccupant.Empty);
        InitialClock = initialClock ?? TimeSpan.FromMinutes(10);
        ClockIncrement = clockIncrement ?? TimeSpan.Zero;
    }

    public void SetClockSettings(TimeSpan initial, TimeSpan increment)
    {
        EnsureNotLocked();

        if (initial <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initial), "Initial clock must be positive.");
        if (increment < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(increment), "Increment can't be negative.");

        InitialClock = initial;
        ClockIncrement = increment;
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
