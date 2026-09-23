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

    public override IReadOnlyList<SeatId> ActiveSeats =>
        [.. StartedGame.SidesOnTheClock.Select(side => new SeatId(side, SeatRole.Player))];

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = new GameState(new Clock(initial, increment));
        TurnStartedAt = now;
    }

    public override void Apply(GameAction action, SeatId seat, DateTimeOffset now)
    {
        var game = StartedGame;

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

        game.Clock.Deduct(seat.Side, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, promoteTo, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    protected override IReadOnlyList<ChessMove> AvailableMoves => StartedGame.AvailableMoves;

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override DraftSectionDto DraftSection
    {
        get
        {
            var game = StartedGame;
            return new DraftSectionDto(
                game.Phase,
                game.SideToPick,
                [.. PoolKinds.Select(kind =>
                    new DraftPoolEntryDto(kind, game.Pool.GetValueOrDefault(kind), game.PriceOf(kind)))],
                game.PicksOf(Side.White),
                game.PicksOf(Side.Black),
                game.BudgetLeft(Side.White),
                game.BudgetLeft(Side.Black),
                game.HasPassed(Side.White),
                game.HasPassed(Side.Black),
                PlacementsOf(game, Side.White),
                PlacementsOf(game, Side.Black),
                game.HasFinishedPlacing(Side.White),
                game.HasFinishedPlacing(Side.Black),
                (int)game.TimeBonusOf(Side.White).TotalSeconds,
                (int)game.TimeBonusOf(Side.Black).TotalSeconds,
                PlaceableSquaresOf(game, Side.White),
                PlaceableSquaresOf(game, Side.Black));
        }
    }

    private static IReadOnlyList<DraftPlaceableDto> PlaceableSquaresOf(GameState game, Side side) =>
    [
        .. PoolKinds.Append(PieceKind.King)
            .Where(kind => game.Remaining(side, kind) > 0)
            .Select(kind => new DraftPlaceableDto(kind, game.SquaresFor(side, kind))),
    ];

    private static IReadOnlyList<DraftPlacementDto> PlacementsOf(GameState game, Side side) =>
        [.. game.PlacementsOf(side).Select(entry => new DraftPlacementDto(entry.Key, entry.Value))];
}
