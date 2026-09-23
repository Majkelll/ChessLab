using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Tests.CardChess;
using ArcaneGameState = ChessLab.Core.ArcaneChess.GameState;
using CardChessGameState = ChessLab.Core.CardChess.GameState;

namespace ChessLab.Core.Tests.ArcaneChess;

// FakeChessRulesEngine.AllMoves only takes effect on the *next* RefreshAvailableMoves — which
// CardChess.GameState runs synchronously inside MakeMove, right after flipping SideToMove. So to
// control what a given turn's AvailableMoves contains, AllMoves must be set *before* the move that
// causes that side's turn to begin, not after. PlayThenOffer below plays the move that's already
// valid per the current (still-cached) AvailableMoves, while queuing up AllMoves for whichever side
// is about to start their turn.
public class GameStateTests
{
    private static readonly ChessMove Dummy = Move("h1", "h2", PieceKind.Rook);

    private static ChessMove Move(string from, string to, PieceKind piece, PieceKind? captured = null) =>
        new(Square.Parse(from), Square.Parse(to), piece, captured, null, false, false, $"{from}{to}");

    private static IReadOnlyList<SpellRank> SpellOrder(params SpellRank[] first) =>
        [.. first, .. SpellRankExtensions.All.Where(s => !first.Contains(s))];

    private static (ArcaneGameState State, FakeChessRulesEngine Engine, CardChessGameState Inner) NewFakeGame(
        IReadOnlyList<SpellRank>? whiteSpells = null, IReadOnlyList<SpellRank>? blackSpells = null,
        IReadOnlyList<ChessMove>? initialMoves = null, bool initialInCheck = false)
    {
        var engine = new FakeChessRulesEngine
        {
            Fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
            InCheck = initialInCheck,
            AllMoves = [.. initialMoves ?? [Dummy]],
            AlwaysHasLegalMove = true, // deterministic hand-dealing; AvailableMoves still comes from MovesByKind/AllMoves
        };
        var inner = new CardChessGameState(engine, new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero),
            new Deck(Deck.AllRanks), new Deck(Deck.AllRanks));
        var state = new ArcaneGameState(inner,
            new SpellDeck(whiteSpells ?? SpellRankExtensions.All),
            new SpellDeck(blackSpells ?? SpellRankExtensions.All));
        return (state, engine, inner);
    }

    /// <summary>Plays <paramref name="moveToPlay"/> (which must already be in the current mover's
    /// AvailableMoves) and arranges for <paramref name="nextTurnOffer"/> to be what the side who
    /// starts their turn next sees as their options.</summary>
    private static void PlayThenOffer(ArcaneGameState state, FakeChessRulesEngine engine,
        ChessMove moveToPlay, IReadOnlyList<ChessMove> nextTurnOffer)
    {
        engine.AllMoves = [.. nextTurnOffer];
        state.MakeMove(moveToPlay.From, moveToPlay.To, moveToPlay.PromoteTo, TimeSpan.Zero);
    }

    private static void AdvanceMana(ArcaneGameState state, FakeChessRulesEngine engine, Side side, int target)
    {
        while (state.ManaOf(side) < target)
            PlayThenOffer(state, engine, Dummy, [Dummy]);
    }

    [Fact]
    public void NewGame_SideToMoveStartsWithOneMana_OtherSideStartsWithZero()
    {
        var (state, _, _) = NewFakeGame();

        Assert.Equal(1, state.ManaOf(Side.White));
        Assert.Equal(0, state.ManaOf(Side.Black));
    }

    [Fact]
    public void NewGame_DealsThreeSpellCardsToEachHand()
    {
        var (state, _, _) = NewFakeGame();

        Assert.Equal(ArcaneGameState.SpellHandSize, state.SpellHandOf(Side.White).Count);
        Assert.Equal(ArcaneGameState.SpellHandSize, state.SpellHandOf(Side.Black).Count);
    }

    [Fact]
    public void CastSpell_NotEnoughMana_Throws()
    {
        var (state, _, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Swap));

        Assert.Throws<InvalidOperationException>(() => state.CastSpell(SpellRank.Swap, default));
    }

    [Fact]
    public void CastSpell_NotInHand_Throws()
    {
        var (state, _, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Shield, SpellRank.FreezeSquare, SpellRank.Peek));

        Assert.Throws<InvalidOperationException>(() => state.CastSpell(SpellRank.Mend, default));
    }

    [Fact]
    public void CastSpell_SecondSpellSameTurn_Throws()
    {
        var (state, _, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Peek, SpellRank.Jam));

        state.CastSpell(SpellRank.Peek, default);

        Assert.Throws<InvalidOperationException>(() => state.CastSpell(SpellRank.Jam, default));
    }

    [Fact]
    public void CastSpell_OnSuccess_ConsumesManaAndRedrawsTheCard()
    {
        var (state, _, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Peek));

        state.CastSpell(SpellRank.Peek, default);

        Assert.Equal(0, state.ManaOf(Side.White));
        Assert.DoesNotContain(SpellRank.Peek, state.SpellHandOf(Side.White));
        Assert.Equal(ArcaneGameState.SpellHandSize, state.SpellHandOf(Side.White).Count);
    }

    [Fact]
    public void Shield_PreventsCaptureOnTargetSquareForOneTurn_ThenExpires()
    {
        var capture = Move("e3", "d2", PieceKind.Bishop, captured: PieceKind.Pawn);
        var quiet = Move("e3", "e4", PieceKind.Bishop);
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Shield));

        state.CastSpell(SpellRank.Shield, new SpellTarget(Primary: Square.Parse("d2")));
        PlayThenOffer(state, engine, Dummy, [capture, quiet]);

        Assert.DoesNotContain(state.AvailableMoves, m => m.To == Square.Parse("d2") && m.CapturedPiece is not null);

        PlayThenOffer(state, engine, quiet, [Dummy]);
        PlayThenOffer(state, engine, Dummy, [capture]);

        Assert.Contains(state.AvailableMoves, m => m.To == Square.Parse("d2"));
    }

    [Fact]
    public void FreezeSquare_PreventsAnyPieceMovingOntoItForOneTurn()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.FreezeSquare));

        state.CastSpell(SpellRank.FreezeSquare, new SpellTarget(Primary: Square.Parse("d5")));
        PlayThenOffer(state, engine, Dummy, [Move("d7", "d5", PieceKind.Pawn), Move("e7", "e5", PieceKind.Pawn)]);

        Assert.DoesNotContain(state.AvailableMoves, m => m.To == Square.Parse("d5"));
        Assert.Contains(state.AvailableMoves, m => m.To == Square.Parse("e5"));
    }

    [Fact]
    public void PinDown_PreventsAllMovesFromThatSquareForOneTurn()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.PinDown));
        AdvanceMana(state, engine, Side.White, 2);

        state.CastSpell(SpellRank.PinDown, new SpellTarget(Primary: Square.Parse("d7")));
        PlayThenOffer(state, engine, Dummy, [Move("d7", "d6", PieceKind.Pawn), Move("e7", "e6", PieceKind.Pawn)]);

        Assert.DoesNotContain(state.AvailableMoves, m => m.From == Square.Parse("d7"));
        Assert.Contains(state.AvailableMoves, m => m.From == Square.Parse("e7"));
    }

    // Regression: a bot (or human) stuck with zero AvailableMoves never gets to move again — the
    // chess engine still sees legal moves so IsGameOver stays false, and BotRunner's Stockfish call
    // throws on an empty candidate list, silently killing that room's bot loop for good (caught by
    // BotRunner's outer catch-all, logged, never retried). Effects must never be allowed to filter
    // the mover down to zero options.
    [Fact]
    public void EffectFiltering_WouldLeaveZeroMoves_FallsBackToTheUnfilteredMoves()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.PinDown));
        AdvanceMana(state, engine, Side.White, 2);

        var onlyLegalMove = Move("d7", "d6", PieceKind.Pawn);
        state.CastSpell(SpellRank.PinDown, new SpellTarget(Primary: Square.Parse("d7")));
        PlayThenOffer(state, engine, Dummy, [onlyLegalMove]);

        Assert.False(state.IsGameOver);
        Assert.Contains(onlyLegalMove, state.AvailableMoves);
    }

    [Fact]
    public void Disarm_AllowsMovementButNotCaptureForOneTurn()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Disarm));
        AdvanceMana(state, engine, Side.White, 2);

        var quietMove = Move("d7", "d6", PieceKind.Pawn);
        var captureMove = Move("d7", "c6", PieceKind.Pawn, captured: PieceKind.Knight);

        state.CastSpell(SpellRank.Disarm, new SpellTarget(Primary: Square.Parse("d7")));
        PlayThenOffer(state, engine, Dummy, [quietMove, captureMove]);

        Assert.Contains(state.AvailableMoves, m => m == quietMove);
        Assert.DoesNotContain(state.AvailableMoves, m => m == captureMove);
    }

    [Fact]
    public void Dispel_RemovesAnActiveEffectAtTheGivenSquare()
    {
        var (state, engine, _) = NewFakeGame(
            whiteSpells: SpellOrder(SpellRank.FreezeSquare),
            blackSpells: SpellOrder(SpellRank.Dispel));

        PlayThenOffer(state, engine, Dummy, [Dummy]);
        PlayThenOffer(state, engine, Dummy, [Dummy]);
        Assert.Equal(2, state.ManaOf(Side.White));

        state.CastSpell(SpellRank.FreezeSquare, new SpellTarget(Primary: Square.Parse("d4")));
        PlayThenOffer(state, engine, Dummy, [Dummy]);
        Assert.Equal(2, state.ManaOf(Side.Black));
        Assert.Single(state.ActiveEffects);

        state.CastSpell(SpellRank.Dispel, new SpellTarget(Primary: Square.Parse("d4")));

        Assert.Empty(state.ActiveEffects);
    }

    [Fact]
    public void Dispel_NoEffectAtSquare_Throws()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Dispel));
        AdvanceMana(state, engine, Side.White, 2);

        Assert.Throws<InvalidOperationException>(
            () => state.CastSpell(SpellRank.Dispel, new SpellTarget(Primary: Square.Parse("d4"))));
    }

    [Fact]
    public void Jam_SkipsOpponentsNextManaGain()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Jam));

        state.CastSpell(SpellRank.Jam, default);
        PlayThenOffer(state, engine, Dummy, [Dummy]);

        Assert.Equal(0, state.ManaOf(Side.Black));
    }

    [Fact]
    public void Feint_ClearsOpponentsPendingRerollBeforeItResolves()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Feint));
        PlayThenOffer(state, engine, Dummy, [Dummy]);

        var blackCard = state.HandOf(Side.Black)[0];
        state.SelectCardsForReroll([blackCard]);
        Assert.NotEmpty(state.PendingRerollOf(Side.Black));

        PlayThenOffer(state, engine, Dummy, [Dummy]);
        state.CastSpell(SpellRank.Feint, default);

        Assert.Empty(state.PendingRerollOf(Side.Black));
    }

    [Fact]
    public void Peek_RevealsOpponentHandThroughTheirTurn_ThenClearsWhenCastersNextTurnBegins()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Peek));

        state.CastSpell(SpellRank.Peek, default);
        Assert.True(state.IsOpponentHandRevealedTo(Side.White));

        PlayThenOffer(state, engine, Dummy, [Dummy]);
        Assert.True(state.IsOpponentHandRevealedTo(Side.White));

        PlayThenOffer(state, engine, Dummy, [Dummy]);
        Assert.False(state.IsOpponentHandRevealedTo(Side.White));
    }

    [Fact]
    public void Mend_RestoresHp_ClampedToStartingMax()
    {
        var (state, engine, inner) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Mend));
        inner.AdjustHp(Side.White, -2);
        AdvanceMana(state, engine, Side.White, 2);

        state.CastSpell(SpellRank.Mend, default);
        Assert.Equal(2, state.HpOf(Side.White));

        inner.AdjustHp(Side.White, 5);
        Assert.Equal(CardChessGameState.StartingHp, state.HpOf(Side.White));
    }

    [Fact]
    public void Restoration_RestoresTwoHp()
    {
        var (state, engine, inner) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Restoration));
        inner.AdjustHp(Side.White, -2);
        AdvanceMana(state, engine, Side.White, 3);

        state.CastSpell(SpellRank.Restoration, default);

        Assert.Equal(CardChessGameState.StartingHp, state.HpOf(Side.White));
    }

    [Fact]
    public void DeepBreath_RefundsHpWhenThisTurnsMoveIsAnEmergencyMove()
    {
        var rescueMove = Move("e1", "f2", PieceKind.King);
        var (state, _, _) = NewFakeGame(
            whiteSpells: SpellOrder(SpellRank.DeepBreath), initialMoves: [rescueMove], initialInCheck: true);

        state.CastSpell(SpellRank.DeepBreath, default);
        state.MakeMove(rescueMove.From, rescueMove.To, null, TimeSpan.Zero);

        Assert.Equal(CardChessGameState.StartingHp, state.HpOf(Side.White));
    }

    [Fact]
    public void SnapSwap_ReplacesOneRankCardImmediately()
    {
        var (state, _, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.SnapSwap));
        var cardToReplace = state.HandOf(Side.White)[0];

        state.CastSpell(SpellRank.SnapSwap, new SpellTarget(HandCard: cardToReplace));

        Assert.DoesNotContain(cardToReplace, state.HandOf(Side.White));
        Assert.Equal(CardChessGameState.HandSize, state.HandOf(Side.White).Count);
    }

    [Fact]
    public void Reshuffle_ReplacesOneOfOpponentsRankCards()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Reshuffle));
        AdvanceMana(state, engine, Side.White, 2);
        var opponentHandBefore = state.HandOf(Side.Black).ToArray();

        state.CastSpell(SpellRank.Reshuffle, default);

        Assert.Equal(CardChessGameState.HandSize, state.HandOf(Side.Black).Count);
        Assert.NotEqual(opponentHandBefore, state.HandOf(Side.Black));
    }

    [Fact]
    public void Swap_ExchangesTwoOwnPieces()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Swap));
        engine.Fen = "8/8/8/8/8/8/8/B3K2R w K - 0 1";
        AdvanceMana(state, engine, Side.White, 2);

        state.CastSpell(SpellRank.Swap, new SpellTarget(Primary: Square.Parse("a1"), Secondary: Square.Parse("h1")));

        Assert.Equal('R', FenBoard.PieceAt(state.ToFen(), Square.Parse("a1")));
        Assert.Equal('B', FenBoard.PieceAt(state.ToFen(), Square.Parse("h1")));
    }

    [Fact]
    public void Swap_TargetingTheKing_Throws()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Swap));
        engine.Fen = "8/8/8/8/8/8/8/B3K2R w K - 0 1";
        AdvanceMana(state, engine, Side.White, 2);

        Assert.Throws<InvalidOperationException>(() =>
            state.CastSpell(SpellRank.Swap, new SpellTarget(Primary: Square.Parse("a1"), Secondary: Square.Parse("e1"))));
    }

    // Regression: Swap had no back-rank guard (unlike Teleport, which does) — swapping a pawn with
    // a piece on rank 1/8 left it there, which the underlying chess engine can't generate moves for
    // and throws deep inside third-party code instead of failing gracefully. Reproduced via a
    // simulation test that played out full random games; this pins the exact minimal repro.
    [Fact]
    public void Swap_WouldLeaveAPawnOnTheBackRank_Throws()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Swap));
        engine.Fen = "8/8/8/8/8/8/P7/R3K3 w K - 0 1";
        AdvanceMana(state, engine, Side.White, 2);
        var fenBefore = state.ToFen();

        Assert.Throws<InvalidOperationException>(() =>
            state.CastSpell(SpellRank.Swap, new SpellTarget(Primary: Square.Parse("a2"), Secondary: Square.Parse("a1"))));

        Assert.Equal(fenBefore, state.ToFen());
    }

    [Fact]
    public void Teleport_MovesOwnPieceToAnEmptySquare()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Teleport));
        engine.Fen = "8/8/8/8/8/8/8/R3K2R w KQ - 0 1";
        AdvanceMana(state, engine, Side.White, 2);

        state.CastSpell(SpellRank.Teleport, new SpellTarget(Primary: Square.Parse("a1"), Secondary: Square.Parse("d4")));

        Assert.Null(FenBoard.PieceAt(state.ToFen(), Square.Parse("a1")));
        Assert.Equal('R', FenBoard.PieceAt(state.ToFen(), Square.Parse("d4")));
    }

    [Fact]
    public void Teleport_PawnCannotLandOnTheBackRank()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Teleport));
        engine.Fen = "8/8/8/8/8/8/P7/4K3 w - - 0 1";
        AdvanceMana(state, engine, Side.White, 2);

        Assert.Throws<InvalidOperationException>(() =>
            state.CastSpell(SpellRank.Teleport, new SpellTarget(Primary: Square.Parse("a2"), Secondary: Square.Parse("a8"))));
    }

    [Fact]
    public void Teleport_WhenItWouldLeaveOwnKingInCheck_ThrowsAndDoesNotConsumeManaOrCard()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Teleport));
        engine.Fen = "8/8/8/8/8/8/8/R3K2R w KQ - 0 1";
        AdvanceMana(state, engine, Side.White, 2);
        engine.InCheck = true; // simulates the post-edit position leaving White's own king in check
        var fenBefore = state.ToFen();

        Assert.Throws<InvalidOperationException>(() =>
            state.CastSpell(SpellRank.Teleport, new SpellTarget(Primary: Square.Parse("a1"), Secondary: Square.Parse("a5"))));

        Assert.Equal(fenBefore, state.ToFen());
        Assert.Equal(2, state.ManaOf(Side.White));
        Assert.Contains(SpellRank.Teleport, state.SpellHandOf(Side.White));
    }

    [Fact]
    public void Execution_RemovesAnOpponentPawn_AndRejectsNonPawnTargets()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.Execution));
        engine.Fen = "4k3/pppppppp/8/8/8/8/8/4K3 w - - 0 1";
        AdvanceMana(state, engine, Side.White, 3);

        Assert.Throws<InvalidOperationException>(
            () => state.CastSpell(SpellRank.Execution, new SpellTarget(Primary: Square.Parse("e8"))));

        state.CastSpell(SpellRank.Execution, new SpellTarget(Primary: Square.Parse("a7")));

        Assert.Null(FenBoard.PieceAt(state.ToFen(), Square.Parse("a7")));
    }

    [Fact]
    public void MindSwap_SwapsKingWithAnotherOwnPiece()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.MindSwap));
        engine.Fen = "8/8/8/8/8/8/8/N3K3 w - - 0 1";
        AdvanceMana(state, engine, Side.White, 3);

        state.CastSpell(SpellRank.MindSwap, new SpellTarget(Primary: Square.Parse("a1")));

        Assert.Equal('K', FenBoard.PieceAt(state.ToFen(), Square.Parse("a1")));
        Assert.Equal('N', FenBoard.PieceAt(state.ToFen(), Square.Parse("e1")));
    }

    // Regression: MindSwap had the same missing back-rank guard as Swap — swapping the king (almost
    // always still on its rank-1/8 home square early in a game) with a pawn leaves that pawn on the
    // back rank.
    [Fact]
    public void MindSwap_WouldLeaveAPawnOnTheBackRank_Throws()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.MindSwap));
        engine.Fen = "8/8/8/8/8/8/P7/4K3 w - - 0 1";
        AdvanceMana(state, engine, Side.White, 3);
        var fenBefore = state.ToFen();

        Assert.Throws<InvalidOperationException>(() =>
            state.CastSpell(SpellRank.MindSwap, new SpellTarget(Primary: Square.Parse("a2"))));

        Assert.Equal(fenBefore, state.ToFen());
    }

    [Fact]
    public void TimeFreeze_FreezesTwoSquaresForOpponentsNextTurn()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.TimeFreeze));
        AdvanceMana(state, engine, Side.White, 3);

        state.CastSpell(SpellRank.TimeFreeze, new SpellTarget(Primary: Square.Parse("d4"), Secondary: Square.Parse("e4")));
        PlayThenOffer(state, engine, Dummy, [Move("d5", "d4", PieceKind.Pawn), Move("d5", "e5", PieceKind.Pawn)]);

        Assert.DoesNotContain(state.AvailableMoves, m => m.To == Square.Parse("d4"));
        Assert.Contains(state.AvailableMoves, m => m.To == Square.Parse("e5"));
    }

    [Fact]
    public void ExtraTurn_GrantsAnImmediateBonusTurnWithoutSwitchingSide_AndBlocksASecondSpellThatTurn()
    {
        var (state, engine, _) = NewFakeGame(whiteSpells: SpellOrder(SpellRank.ExtraTurn, SpellRank.Mend));
        AdvanceMana(state, engine, Side.White, 3);

        state.CastSpell(SpellRank.ExtraTurn, default);
        Assert.Throws<InvalidOperationException>(() => state.CastSpell(SpellRank.Mend, default));

        PlayThenOffer(state, engine, Dummy, [Dummy]);
        Assert.Equal(Side.White, state.SideToMove);
        Assert.Throws<InvalidOperationException>(() => state.CastSpell(SpellRank.Mend, default));

        PlayThenOffer(state, engine, Dummy, [Dummy]);
        Assert.Equal(Side.Black, state.SideToMove);
    }
}
