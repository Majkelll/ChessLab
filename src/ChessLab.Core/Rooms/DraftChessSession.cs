using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;
using GameState = ChessLab.Core.Draft.GameState;

namespace ChessLab.Core.Rooms;

public sealed class DraftChessSession(Room room) : RoomSession<GameState>(room)
{
    private static readonly PieceKind[] PoolKinds =
        [PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.Pawn];

    private static readonly Side[] BothSides = [Side.White, Side.Black];

    private readonly Dictionary<Side, DateTimeOffset> placingSince = [];

    public override IReadOnlyList<SeatId> ActiveSeats => SeatsOf(StartedGame.SidesOnTheClock);

    public override IReadOnlyList<SeatId> SeatsAllowedToAct =>
        StartedGame is { Phase: DraftPhase.Placing, IsGameOver: false } ? SeatsOf(BothSides) : ActiveSeats;

    private static IReadOnlyList<SeatId> SeatsOf(IEnumerable<Side> sides) =>
        [.. sides.Select(side => new SeatId(side, SeatRole.Player))];

    protected override DateTimeOffset? TurnStartOf(Side side) =>
        Game is { Phase: DraftPhase.Placing } && placingSince.TryGetValue(side, out var since) ? since : TurnStartedAt;

    protected override void ShiftTurnTimers(TimeSpan by)
    {
        base.ShiftTurnTimers(by);

        foreach (var side in placingSince.Keys.ToArray())
            placingSince[side] += by;
    }

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = new GameState(new Clock(initial, increment));
        TurnStartedAt = now;
    }

    protected override void ApplyAction(GameAction action, SeatId seat, DateTimeOffset now)
    {
        var game = StartedGame;
        var phaseBefore = game.Phase;
        var timerStart = game.SidesOnTheClock.Contains(seat.Side) ? TurnStartOf(seat.Side) : null;

        switch (action.Kind)
        {
            case GameActionKind.DraftPick:
                game.Pick(seat.Side, action.SelectedKind!.Value);
                break;
            case GameActionKind.DraftPass:
                game.Pass(seat.Side);
                break;
            case GameActionKind.PlacePiece:
                game.Place(seat.Side, action.SelectedKind!.Value, action.To!.Value);
                break;
            case GameActionKind.UnplacePiece:
                game.Unplace(seat.Side, action.To!.Value);
                break;
            case GameActionKind.Move:
                MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
                return;
            default:
                throw new InvalidOperationException($"{action.Kind} is not an action in Draft Chess.");
        }

        if (timerStart is { } startedAt)
            game.Clock.Deduct(seat.Side, now - startedAt);

        RestartTimers(game, phaseBefore, seat.Side, now);
    }

    private void RestartTimers(GameState game, DraftPhase phaseBefore, Side actor, DateTimeOffset now)
    {
        TurnStartedAt = now;

        if (game.Phase != DraftPhase.Placing)
            return;

        if (phaseBefore == DraftPhase.Placing)
            placingSince[actor] = now;
        else
            placingSince[Side.White] = placingSince[Side.Black] = now;
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, promoteTo, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    protected override IReadOnlyList<ChessMove> AvailableMoves => StartedGame.AvailableMoves;

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override DraftSectionDto DraftSectionFor(Side? viewer)
    {
        var game = StartedGame;
        return new DraftSectionDto(
            game.Phase,
            game.SideToPick,
            [.. PoolKinds.Select(kind =>
                new DraftPoolEntryDto(kind, game.Pool.GetValueOrDefault(kind), game.PriceOf(kind)))],
            [.. game.PicksOf(Side.White)],
            [.. game.PicksOf(Side.Black)],
            game.BudgetLeft(Side.White),
            game.BudgetLeft(Side.Black),
            game.HasPassed(Side.White),
            game.HasPassed(Side.Black),
            SeesLayoutOf(viewer, Side.White) ? PlacementsOf(game, Side.White) : [],
            SeesLayoutOf(viewer, Side.Black) ? PlacementsOf(game, Side.Black) : [],
            game.HasFinishedPlacing(Side.White),
            game.HasFinishedPlacing(Side.Black),
            (int)game.TimeBonusOf(Side.White).TotalSeconds,
            (int)game.TimeBonusOf(Side.Black).TotalSeconds,
            SeesLayoutOf(viewer, Side.White) ? PlaceableSquaresOf(game, Side.White) : [],
            SeesLayoutOf(viewer, Side.Black) ? PlaceableSquaresOf(game, Side.Black) : []);
    }

    private bool SeesLayoutOf(Side? viewer, Side owner) =>
        StartedGame.Phase == DraftPhase.Playing || StartedGame.IsGameOver || viewer == owner;

    private static IReadOnlyList<DraftPlaceableDto> PlaceableSquaresOf(GameState game, Side side) =>
    [
        .. PoolKinds.Append(PieceKind.King)
            .Where(kind => game.Remaining(side, kind) > 0)
            .Select(kind => new DraftPlaceableDto(kind, game.SquaresFor(side, kind))),
    ];

    private static IReadOnlyList<DraftPlacementDto> PlacementsOf(GameState game, Side side) =>
        [.. game.PlacementsOf(side).Select(entry => new DraftPlacementDto(entry.Key, entry.Value))];
}
