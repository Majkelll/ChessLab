using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using GameState = ChessLab.Core.Absorption.GameState;

namespace ChessLab.Core.Rooms;

public sealed class AbsorptionChessSession(Room room) : RoomSession<GameState>(room)
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
            throw new InvalidOperationException($"{action.Kind} is not an action in Absorption Chess.");

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

    protected override AbsorptionSectionDto AbsorptionSection
    {
        get
        {
            var game = StartedGame;
            return new AbsorptionSectionDto(game.Fen, game.PowersText, game.IsInCheck(game.SideToMove));
        }
    }
}
