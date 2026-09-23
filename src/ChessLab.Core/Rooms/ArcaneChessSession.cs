using ChessLab.Core.ArcaneChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using GameState = ChessLab.Core.ArcaneChess.GameState;

namespace ChessLab.Core.Rooms;

public sealed class ArcaneChessSession(Room room, Func<IChessRulesEngine> engineFactory) : RoomSession<GameState>(room)
{
    public ArcaneChessSession(Room room) : this(room, static () => new GeraChessRulesEngine())
    {
    }

    public override IReadOnlyList<SeatId> ActiveSeats => [new SeatId(StartedGame.SideToMove, SeatRole.Player)];

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        var rng = random ?? Random.Shared;
        var inner = new CardChess.GameState(engineFactory(), new Clock(initial, increment),
            CardChess.Deck.Shuffled(rng), CardChess.Deck.Shuffled(rng));
        Game = new GameState(inner, SpellDeck.Shuffled(rng), SpellDeck.Shuffled(rng));
        TurnStartedAt = now;
    }

    public override void Apply(GameAction action, SeatId seat, DateTimeOffset now)
    {
        switch (action.Kind)
        {
            case GameActionKind.Move:
                MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
                break;
            case GameActionKind.SelectReroll:
                SelectCardsForReroll(action.Cards!);
                break;
            case GameActionKind.CastSpell:
                CastSpell(action.Spell!.Value, action.Target!.Value);
                break;
            default:
                throw new InvalidOperationException($"{action.Kind} is not an action in Arcane Chess.");
        }
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, promoteTo, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    public void CastSpell(SpellRank spell, SpellTarget target) => StartedGame.CastSpell(spell, target);

    public void SelectCardsForReroll(IReadOnlyList<CardChess.CardRank> cards) =>
        StartedGame.SelectCardsForReroll(cards);

    protected override IReadOnlyList<ChessMove> AvailableMoves => StartedGame.AvailableMoves;

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override CardChessSectionDto CardChessSection
    {
        get
        {
            var game = StartedGame;
            return new CardChessSectionDto(
                game.HandOf(Side.White),
                game.HandOf(Side.Black),
                game.PendingRerollOf(Side.White),
                game.PendingRerollOf(Side.Black),
                game.EmergencyMoveAvailable,
                game.HandHasNoPlayableCard,
                game.HpOf(Side.White),
                game.HpOf(Side.Black));
        }
    }

    protected override ArcaneChessSectionDto ArcaneSection
    {
        get
        {
            var game = StartedGame;
            return new ArcaneChessSectionDto(
                game.SpellHandOf(Side.White),
                game.SpellHandOf(Side.Black),
                game.ManaOf(Side.White),
                game.ManaOf(Side.Black),
                game.IsOpponentHandRevealedTo(Side.White),
                game.IsOpponentHandRevealedTo(Side.Black),
                game.ActiveEffects);
        }
    }
}
