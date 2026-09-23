using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using GameState = ChessLab.Core.Progressive.GameState;

namespace ChessLab.Core.Rooms;

public sealed class ProgressiveChessSession(Room room, Func<IChessRulesEngine> engineFactory)
    : RoomSession<GameState>(room)
{
    public ProgressiveChessSession(Room room) : this(room, static () => new GeraChessRulesEngine())
    {
    }

    public override IReadOnlyList<SeatId> ActiveSeats => [new SeatId(StartedGame.SideToMove, SeatRole.Player)];

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = new GameState(engineFactory(), new Clock(initial, increment));
        TurnStartedAt = now;
    }

    public override void Apply(GameAction action, SeatId seat, DateTimeOffset now)
    {
        if (action.Kind != GameActionKind.Move)
            throw new InvalidOperationException($"{action.Kind} is not an action in Progressive Chess.");

        MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, promoteTo, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    protected override IReadOnlyList<ChessMove> AvailableMoves => StartedGame.AvailableMoves;

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override ProgressiveSectionDto ProgressiveSection
    {
        get
        {
            var game = StartedGame;
            return new ProgressiveSectionDto(game.SeriesNumber, game.MovesPlayedInSeries, game.MovesLeftInSeries);
        }
    }
}
