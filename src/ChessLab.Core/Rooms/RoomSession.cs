using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;

namespace ChessLab.Core.Rooms;

/// <summary>Everything a session does that has nothing to do with a particular game's rules:
/// holding the room and the started game, charging the clock for the time a turn took, and turning
/// the state into the wire shape. Subclasses supply the rules and their own section of that shape.</summary>
public abstract class RoomSession<TGame>(Room room) : IRoomSession
    where TGame : class, IGameEngineState
{
    public Room Room { get; } = room;

    public TGame? Game { get; protected set; }

    IGameEngineState? IRoomSession.Game => Game;

    public DateTimeOffset? TurnStartedAt { get; protected set; }

    public bool HasActiveGame => Game is { IsGameOver: false };

    public abstract IReadOnlyList<SeatId> ActiveSeats { get; }

    public abstract void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null);

    public abstract void Apply(GameAction action, SeatId seat, DateTimeOffset now);

    protected abstract IReadOnlyList<ChessMove> AvailableMoves { get; }

    protected abstract IReadOnlyList<ChessMove> MoveHistory { get; }

    protected virtual HandBrainSectionDto? HandBrainSection => null;

    protected virtual CardChessSectionDto? CardChessSection => null;

    protected virtual ArcaneChessSectionDto? ArcaneSection => null;

    protected virtual BiddingSectionDto? BiddingSection => null;

    protected virtual ProgressiveSectionDto? ProgressiveSection => null;

    public void DeclareTimeoutIfExpired(DateTimeOffset now)
    {
        if (Game is not { IsGameOver: false } game || TurnStartedAt is not { } startedAt)
            return;

        var elapsedSinceTurnStart = now - startedAt;
        foreach (var side in game.SidesOnTheClock)
        {
            if (elapsedSinceTurnStart >= game.Clock.Remaining(side))
                game.Clock.Deduct(side, elapsedSinceTurnStart);
        }

        game.DeclareTimeoutIfFlagged();
    }

    public GameStateEnvelopeDto ToStateDto()
    {
        var game = StartedGame;
        return new GameStateEnvelopeDto(
            Room.Kind,
            game.SideToMove,
            game.PositionText,
            AvailableMoves,
            MoveHistory,
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner,
            HandBrainSection,
            CardChessSection,
            ArcaneSection,
            BiddingSection,
            ProgressiveSection);
    }

    public GameUpdateEnvelopeDto ToUpdateDto()
    {
        var game = StartedGame;
        var history = MoveHistory;
        return new GameUpdateEnvelopeDto(
            Room.Kind,
            game.SideToMove,
            game.PositionText,
            AvailableMoves,
            history.Count > 0 ? history[^1] : null,
            history.Count,
            (long)game.Clock.WhiteRemaining.TotalMilliseconds,
            (long)game.Clock.BlackRemaining.TotalMilliseconds,
            game.IsGameOver,
            game.EndResult?.Reason,
            game.EndResult?.Winner,
            HandBrainSection,
            CardChessSection,
            ArcaneSection,
            BiddingSection,
            ProgressiveSection);
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
