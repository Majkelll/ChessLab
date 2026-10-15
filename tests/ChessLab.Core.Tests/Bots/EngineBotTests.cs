using ChessLab.Bots;
using ChessLab.Core.ArcaneChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Bots;

public class EngineBotTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);
    private static readonly SeatId WhiteBrain = new(Side.White, SeatRole.Brain);
    private static readonly SeatId WhiteHand = new(Side.White, SeatRole.Hand);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Room TwoSeatRoom(GameKind kind)
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), kind);
        room.SetBot(White, BotDifficulty.Medium);
        room.ClaimSeat(Black, Guid.NewGuid(), "Bob");
        return room;
    }

    private static GameSession StartedHandAndBrain()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.SetBot(WhiteBrain, BotDifficulty.Hard);
        room.SetBot(WhiteHand, BotDifficulty.Hard);
        room.ClaimSeat(new SeatId(Side.Black, SeatRole.Brain), Guid.NewGuid(), "Cai");
        room.ClaimSeat(new SeatId(Side.Black, SeatRole.Hand), Guid.NewGuid(), "Deb");
        var session = new GameSession(room);
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);
        return session;
    }

    private static void AssertIsAvailableMove(GameStateEnvelopeDto state, GameAction action)
    {
        Assert.Equal(GameActionKind.Move, action.Kind);
        Assert.Contains(state.AvailableMoves, move =>
            move.From == action.From && move.To == action.To && move.PromoteTo == action.PromoteTo);
    }

    [Fact]
    public async Task CardChessBot_PlaysOneOfTheAvailableMoves()
    {
        var session = new CardChessSession(TwoSeatRoom(GameKind.CardChess));
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start, 3);
        var state = session.ToStateDto(Side.White);
        var engine = new FakeChessEngine();

        var action = await new CardChessBot(engine).ChooseActionAsync(state, White, BotDifficulty.Medium);

        AssertIsAvailableMove(state, action);
        var search = Assert.Single(engine.Searches);
        Assert.Equal(state.PositionText, search.Fen);
        Assert.Equal((1700, 500), (search.Elo, search.MovetimeMs));
    }

    [Fact]
    public async Task ProgressiveChessBot_PlaysOneOfTheAvailableMoves()
    {
        var session = new ProgressiveChessSession(TwoSeatRoom(GameKind.ProgressiveChess));
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);
        var state = session.ToStateDto(Side.White);

        var action = await new ProgressiveChessBot(new FakeChessEngine()).ChooseActionAsync(state, White, BotDifficulty.Easy);

        AssertIsAvailableMove(state, action);
    }

    [Fact]
    public async Task HandAndBrainBot_AsHand_PlaysAMoveOfTheAnnouncedKind()
    {
        var session = StartedHandAndBrain();
        session.Apply(GameAction.SelectPieceKind(PieceKind.Knight), WhiteBrain, Start);
        var state = session.ToStateDto(Side.White);

        var action = await new HandBrainBot(new FakeChessEngine()).ChooseActionAsync(state, WhiteHand, BotDifficulty.Hard);

        AssertIsAvailableMove(state, action);
        Assert.All(state.AvailableMoves, move => Assert.Equal(PieceKind.Knight, move.Piece));
    }

    [Fact]
    public async Task HandAndBrainBot_AsBrain_AnnouncesTheKindWithTheBestScoreFromItsOwnLegalMoves()
    {
        var session = StartedHandAndBrain();
        var state = session.ToStateDto(Side.White);
        var engine = new FakeChessEngine(candidates => candidates.Any(uci => uci.StartsWith("g1")) ? 50 : 10);

        var action = await new HandBrainBot(engine).ChooseActionAsync(state, WhiteBrain, BotDifficulty.Hard);

        Assert.Equal(GameAction.SelectPieceKind(PieceKind.Knight), action);
        Assert.Equal(state.HandBrain!.AvailablePieceKinds.Count, engine.Searches.Count);
        Assert.All(engine.Searches, search => Assert.Equal(200, search.MovetimeMs));
        Assert.Contains(engine.Searches, search =>
            search.Candidates.Order().SequenceEqual(["b1a3", "b1c3", "g1f3", "g1h3"]));
    }

    [Fact]
    public async Task HandAndBrainBot_AsBrain_OnATieKeepsTheEarlierKind()
    {
        var state = StartedHandAndBrain().ToStateDto(Side.White);

        var action = await new HandBrainBot(new FakeChessEngine(_ => 0)).ChooseActionAsync(state, WhiteBrain, BotDifficulty.Hard);

        Assert.Equal(GameAction.SelectPieceKind(state.HandBrain!.AvailablePieceKinds[0]), action);
        Assert.Equal(TurnPhase.BrainSelecting, state.HandBrain.Phase);
    }

    private static GameStateEnvelopeDto ArcaneState(IReadOnlyList<SpellRank> hand, int mana, int hp = 3,
        bool emergency = false, bool hasCast = false)
    {
        var session = new ArcaneChessSession(TwoSeatRoom(GameKind.ArcaneChess));
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start, 3);
        var state = session.ToStateDto(Side.White);
        return state with
        {
            Arcane = state.Arcane! with { WhiteSpellHand = hand, WhiteMana = mana, SideToMoveHasCast = hasCast },
            CardChess = state.CardChess! with { WhiteHp = hp, EmergencyMoveAvailable = emergency },
        };
    }

    [Fact]
    public async Task ArcaneBot_AfterCastingThisTurn_Moves()
    {
        var state = ArcaneState([SpellRank.DeepBreath], mana: 3, emergency: true, hasCast: true);

        var action = await new ArcaneChessBot(new FakeChessEngine()).ChooseActionAsync(state, White, BotDifficulty.Easy);

        AssertIsAvailableMove(state, action);
    }

    [Fact]
    public async Task ArcaneBot_WithAnEmergencyMovePending_CastsDeepBreath()
    {
        var state = ArcaneState([SpellRank.Shield, SpellRank.DeepBreath], mana: 1, emergency: true);

        var action = await new ArcaneChessBot(new FakeChessEngine()).ChooseActionAsync(state, White, BotDifficulty.Easy);

        Assert.Equal(GameAction.CastSpell(SpellRank.DeepBreath, default), action);
    }

    [Fact]
    public void ArcaneBot_BelowFullHp_PrefersRestorationOverMend()
    {
        var bot = new ArcaneChessBot(new FakeChessEngine());

        Assert.Equal(SpellRank.Restoration, bot.ChooseSpell(ArcaneState([SpellRank.Mend, SpellRank.Restoration], mana: 3, hp: 2), Side.White)!.Value.Spell);
        Assert.Equal(SpellRank.Mend, bot.ChooseSpell(ArcaneState([SpellRank.Mend, SpellRank.Restoration], mana: 2, hp: 2), Side.White)!.Value.Spell);
    }

    [Fact]
    public void ArcaneBot_NeverCastsTheSpellsItIsNotAllowedToCast()
    {
        var bot = new ArcaneChessBot(new FakeChessEngine());
        var state = ArcaneState([SpellRank.Swap, SpellRank.Teleport, SpellRank.Execution], mana: 3);

        for (var i = 0; i < 100; i++)
            Assert.Null(bot.ChooseSpell(state, Side.White));
    }

    [Fact]
    public void ArcaneBot_ShieldsOnlyItsOwnPiecesOtherThanTheKing()
    {
        var bot = new ArcaneChessBot(new FakeChessEngine());
        var state = ArcaneState([SpellRank.Shield], mana: 1);
        var casts = Enumerable.Range(0, 200).Select(_ => bot.ChooseSpell(state, Side.White)).OfType<(SpellRank Spell, SpellTarget Target)>().ToList();

        Assert.NotEmpty(casts);
        Assert.All(casts, cast =>
        {
            var piece = FenBoard.PieceAt(state.PositionText, cast.Target.Primary!.Value);
            Assert.True(piece is { } c && char.IsUpper(c) && c != 'K');
        });
    }

    [Fact]
    public void ArcaneBot_DisarmsOnlyOpposingPiecesOtherThanTheKing()
    {
        var bot = new ArcaneChessBot(new FakeChessEngine());
        var state = ArcaneState([SpellRank.Disarm], mana: 2);
        var casts = Enumerable.Range(0, 200).Select(_ => bot.ChooseSpell(state, Side.White)).OfType<(SpellRank Spell, SpellTarget Target)>().ToList();

        Assert.NotEmpty(casts);
        Assert.All(casts, cast =>
        {
            var piece = FenBoard.PieceAt(state.PositionText, cast.Target.Primary!.Value);
            Assert.True(piece is { } c && char.IsLower(c) && c != 'k');
        });
    }

    [Fact]
    public void ArcaneBot_SnapSwapsACardFromItsOwnHand()
    {
        var bot = new ArcaneChessBot(new FakeChessEngine());
        var state = ArcaneState([SpellRank.SnapSwap], mana: 1);
        var casts = Enumerable.Range(0, 200).Select(_ => bot.ChooseSpell(state, Side.White)).OfType<(SpellRank Spell, SpellTarget Target)>().ToList();

        Assert.NotEmpty(casts);
        Assert.All(casts, cast => Assert.Contains(cast.Target.HandCard!.Value, state.CardChess!.WhiteHand));
    }
}
