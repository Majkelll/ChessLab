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
        var rng = random ?? Random.Shared;
        StartWith(() => new GameState(
            new CardChess.GameState(engineFactory(), new Clock(initial, increment),
                CardChess.Deck.Shuffled(rng), CardChess.Deck.Shuffled(rng)),
            SpellDeck.Shuffled(rng), SpellDeck.Shuffled(rng), rng), now);
    }

    internal void StartWith(Func<GameState> deal, DateTimeOffset now)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = deal();
        TurnStartedAt = now;
    }

    protected override void ApplyAction(GameAction action, SeatId seat, DateTimeOffset now)
    {
        switch (action.Kind)
        {
            case GameActionKind.Move:
                MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
                break;
            case GameActionKind.SelectReroll:
                SelectCardsForReroll(action.Cards ?? throw new InvalidOperationException("A reroll selection needs a list of cards."));
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

    protected override IReadOnlyList<ChessMove> AvailableMovesFor(Side? viewer) =>
        StartedGame.IsGameOver || viewer == StartedGame.SideToMove ? AvailableMoves : [];

    protected override CardChessSectionDto CardChessSectionFor(Side? viewer)
    {
        var game = StartedGame;
        return new CardChessSectionDto(
            RankHandShownTo(viewer, Side.White),
            RankHandShownTo(viewer, Side.Black),
            SeesRankHandOf(viewer, Side.White) ? [.. game.PendingRerollOf(Side.White)] : [],
            SeesRankHandOf(viewer, Side.Black) ? [.. game.PendingRerollOf(Side.Black)] : [],
            game.EmergencyMoveAvailable,
            game.HandHasNoPlayableCard,
            game.HpOf(Side.White),
            game.HpOf(Side.Black));
    }

    protected override ArcaneChessSectionDto ArcaneSectionFor(Side? viewer)
    {
        var game = StartedGame;
        return new ArcaneChessSectionDto(
            SeesSpellHandOf(viewer, Side.White) ? [.. game.SpellHandOf(Side.White)] : [],
            SeesSpellHandOf(viewer, Side.Black) ? [.. game.SpellHandOf(Side.Black)] : [],
            game.ManaOf(Side.White),
            game.ManaOf(Side.Black),
            game.IsOpponentHandRevealedTo(Side.White),
            game.IsOpponentHandRevealedTo(Side.Black),
            [.. game.ActiveEffects],
            game.HasCastSpellThisTurn(game.SideToMove));
    }

    private IReadOnlyList<CardChess.CardRank> RankHandShownTo(Side? viewer, Side owner) =>
        SeesRankHandOf(viewer, owner) ? [.. StartedGame.HandOf(owner)] : [];

    private bool SeesSpellHandOf(Side? viewer, Side owner) => StartedGame.IsGameOver || viewer == owner;

    private bool SeesRankHandOf(Side? viewer, Side owner) =>
        SeesSpellHandOf(viewer, owner) || (viewer is { } side && StartedGame.IsOpponentHandRevealedTo(side));
}
