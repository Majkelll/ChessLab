using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using GameState = ChessLab.Core.Martian.GameState;

namespace ChessLab.Core.Rooms;

public sealed class MartianChessSession(Room room) : RoomSession<GameState>(room)
{
    public override IReadOnlyList<SeatId> ActiveSeats => [new SeatId(StartedGame.SideToMove, SeatRole.Player)];

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = new GameState(new Clock(initial, increment));
        TurnStartedAt = now;
    }

    public override void Apply(GameAction action, SeatId seat, DateTimeOffset now)
    {
        if (action.Kind != GameActionKind.Move)
            throw new InvalidOperationException($"{action.Kind} is not an action in Martian Chess.");

        MakeMove(action.From!.Value, action.To!.Value, now);
    }

    public ChessMove MakeMove(Square from, Square to, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, null, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    protected override IReadOnlyList<ChessMove> AvailableMoves => StartedGame.AvailableMoves;

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override MartianSectionDto MartianSection
    {
        get
        {
            var game = StartedGame;
            var pyramids = new List<MartianPyramidDto>();

            for (var file = 0; file < GameState.Files; file++)
            {
                for (var rank = 0; rank < GameState.Ranks; rank++)
                {
                    var square = new Square(file, rank);
                    if (game.PyramidAt(square) is var value && value != 0)
                        pyramids.Add(new MartianPyramidDto(square, value));
                }
            }

            return new MartianSectionDto(pyramids, game.ScoreOf(Side.White), game.ScoreOf(Side.Black));
        }
    }
}
