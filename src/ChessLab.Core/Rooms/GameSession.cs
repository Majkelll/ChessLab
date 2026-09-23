using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.Rooms;

/// <summary>Ties a lobby <see cref="Room"/> to its (possibly not-yet-started) <see cref="GameState"/>.</summary>
public sealed class GameSession(Room room, Func<IChessRulesEngine> engineFactory) : RoomSession<GameState>(room)
{
    public GameSession(Room room) : this(room, static () => new GeraChessRulesEngine())
    {
    }

    /// <summary>The seat whose occupant is expected to act right now (Brain to announce, or Hand to move).</summary>
    public override SeatId ActiveSeat
    {
        get
        {
            var game = StartedGame;
            var role = game.Phase == TurnPhase.BrainSelecting ? SeatRole.Brain : SeatRole.Hand;
            return new SeatId(game.SideToMove, role);
        }
    }

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = new GameState(engineFactory(), new Clock(initial, increment));
        TurnStartedAt = now;
    }

    public override void Apply(GameAction action, DateTimeOffset now)
    {
        switch (action.Kind)
        {
            case GameActionKind.SelectPieceKind:
                StartedGame.SelectPieceKind(action.SelectedKind!.Value);
                break;
            case GameActionKind.Move:
                MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
                break;
            default:
                throw new InvalidOperationException($"{action.Kind} is not an action in Hand & Brain.");
        }
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, promoteTo, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    protected override IReadOnlyList<ChessMove> AvailableMoves =>
        Game is { IsGameOver: false, Phase: TurnPhase.HandMoving } game ? game.AvailableMoves() : [];

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override HandBrainSectionDto HandBrainSection
    {
        get
        {
            var game = StartedGame;
            return new HandBrainSectionDto(
                game.Phase,
                game.SelectedPieceKind,
                game is { IsGameOver: false, Phase: TurnPhase.BrainSelecting } ? game.AvailablePieceKinds() : []);
        }
    }
}
