using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;

namespace ChessLab.Core.Rooms;

public abstract class RoomSession<TGame>(Room room) : IRoomSession
    where TGame : class, IGameEngineState
{
    public Room Room { get; } = room;

    public TGame? Game { get; protected set; }

    IGameEngineState? IRoomSession.Game => Game;

    public DateTimeOffset? TurnStartedAt { get; protected set; }

    public bool HasActiveGame => Game is { IsGameOver: false };

    public DateTimeOffset? PausedAt { get; private set; }

    public bool IsPaused => PausedAt is not null && HasActiveGame;

    public DateTimeOffset? PauseDeadline => IsPaused ? PausedAt + GamePause.Limit : null;

    public abstract IReadOnlyList<SeatId> ActiveSeats { get; }

    public virtual IReadOnlyList<SeatId> SeatsAllowedToAct => ActiveSeats;

    public abstract void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null);

    public void Apply(GameAction action, SeatId seat, DateTimeOffset now)
    {
        if (IsPaused)
            throw new InvalidOperationException(GamePause.Rejection);

        ApplyAction(action, seat, now);
    }

    public void ApplyBotAction(GameAction action, SeatId seat, DateTimeOffset askedAt, DateTimeOffset now)
    {
        if (!IsPaused || PausedAt is not { } pausedAt)
        {
            ApplyAction(action, seat, now);
            return;
        }

        var pausedFor = (askedAt > pausedAt ? askedAt : pausedAt) - pausedAt;
        ShiftTurnTimers(pausedFor);
        PausedAt = null;

        try
        {
            ApplyAction(action, seat, now);
        }
        catch
        {
            ShiftTurnTimers(-pausedFor);
            PausedAt = pausedAt;
            throw;
        }
    }

    public void Pause(DateTimeOffset now)
    {
        if (HasActiveGame && !IsPaused)
            PausedAt = now;
    }

    public bool EndIfPauseExpired(DateTimeOffset now)
    {
        if (PauseDeadline is not { } deadline || now < deadline)
            return false;

        EndBecauseBotUnavailable();
        return true;
    }

    public void EndBecauseBotUnavailable()
    {
        if (Game is not { IsGameOver: false } game)
            return;

        PausedAt = null;
        game.EndBecauseBotUnavailable(WinnerWhenBotUnavailable());
    }

    private Side? WinnerWhenBotUnavailable()
    {
        var sidesWithBots = Room.Seats
            .Where(seat => seat.Value.Kind == OccupantKind.Bot)
            .Select(seat => seat.Key.Side)
            .ToHashSet();

        return sidesWithBots.Count == 1 ? sidesWithBots.Single().Opposite() : null;
    }

    protected abstract void ApplyAction(GameAction action, SeatId seat, DateTimeOffset now);

    protected virtual void ShiftTurnTimers(TimeSpan by) => TurnStartedAt += by;

    protected abstract IReadOnlyList<ChessMove> AvailableMoves { get; }

    protected abstract IReadOnlyList<ChessMove> MoveHistory { get; }

    protected virtual HandBrainSectionDto? HandBrainSection => null;

    protected virtual CardChessSectionDto? CardChessSection => null;

    protected virtual ArcaneChessSectionDto? ArcaneSection => null;

    protected virtual BiddingSectionDto? BiddingSection => null;

    protected virtual ProgressiveSectionDto? ProgressiveSection => null;

    protected virtual AliceSectionDto? AliceSection => null;

    protected virtual AbsorptionSectionDto? AbsorptionSection => null;

    protected virtual MartianSectionDto? MartianSection => null;

    protected virtual DraftSectionDto? DraftSection => null;

    protected virtual IReadOnlyList<ChessMove> AvailableMovesFor(Side? viewer) => AvailableMoves;

    protected virtual CardChessSectionDto? CardChessSectionFor(Side? viewer) => CardChessSection;

    protected virtual ArcaneChessSectionDto? ArcaneSectionFor(Side? viewer) => ArcaneSection;

    protected virtual DraftSectionDto? DraftSectionFor(Side? viewer) => DraftSection;

    protected virtual DateTimeOffset? TurnStartOf(Side side) => TurnStartedAt;

    public void DeclareTimeoutIfExpired(DateTimeOffset now)
    {
        if (Game is not { IsGameOver: false } game || IsPaused)
            return;

        foreach (var side in game.SidesOnTheClock)
        {
            if (TurnStartOf(side) is not { } startedAt)
                continue;

            var elapsed = now - startedAt;
            if (elapsed >= game.Clock.Remaining(side))
                game.Clock.Deduct(side, elapsed);
        }

        game.DeclareTimeoutIfFlagged();
    }

    public GameStateEnvelopeDto ToStateDto(Side? viewer)
    {
        var game = StartedGame;
        return new GameStateEnvelopeDto(
            Room.Kind,
            game.SideToMove,
            game.PositionText,
            AvailableMovesFor(viewer),
            [.. MoveHistory],
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner,
            HandBrainSection,
            CardChessSectionFor(viewer),
            ArcaneSectionFor(viewer),
            BiddingSection,
            ProgressiveSection,
            AliceSection,
            AbsorptionSection,
            MartianSection,
            DraftSectionFor(viewer),
            PauseDeadline);
    }

    public GameUpdateEnvelopeDto ToUpdateDto(Side? viewer)
    {
        var game = StartedGame;
        var history = MoveHistory;
        return new GameUpdateEnvelopeDto(
            Room.Kind,
            game.SideToMove,
            game.PositionText,
            AvailableMovesFor(viewer),
            history.Count > 0 ? history[^1] : null,
            history.Count,
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner,
            HandBrainSection,
            CardChessSectionFor(viewer),
            ArcaneSectionFor(viewer),
            BiddingSection,
            ProgressiveSection,
            AliceSection,
            AbsorptionSection,
            MartianSection,
            DraftSectionFor(viewer),
            PauseDeadline);
    }

    protected TGame StartedGame => Game ?? throw new InvalidOperationException("Game has not started.");

    protected TimeSpan ElapsedSinceTurnStart(DateTimeOffset now) =>
        TurnStartedAt is { } startedAt ? now - startedAt : TimeSpan.Zero;

    protected void EnsureNotStarted()
    {
        if (Game is not null)
            throw new InvalidOperationException("Game has already started.");
    }
}
