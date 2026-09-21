using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;
using GameState = ChessLab.Core.CardChess.GameState;

namespace ChessLab.Core.Tests.CardChess;

public class GameStateTests
{
    private static ChessMove Move(string from, string to, PieceKind piece,
        PieceKind? captured = null, PieceKind? promoteTo = null) =>
        new(Square.Parse(from), Square.Parse(to), piece, captured, promoteTo, false, false, $"{from}{to}");

    private static GameState NewRealGame(IReadOnlyList<CardRank>? whiteOrder = null, IReadOnlyList<CardRank>? blackOrder = null) =>
        new(new GeraChessRulesEngine(), new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero),
            new Deck(whiteOrder ?? Deck.AllRanks), new Deck(blackOrder ?? Deck.AllRanks));

    private static (GameState State, FakeChessRulesEngine Engine) NewFakeGame(
        IReadOnlyList<CardRank>? whiteOrder = null, Action<FakeChessRulesEngine>? configure = null)
    {
        var engine = new FakeChessRulesEngine();
        configure?.Invoke(engine);
        var state = new GameState(engine, new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero),
            new Deck(whiteOrder ?? Deck.AllRanks), new Deck(Deck.AllRanks));
        return (state, engine);
    }

    [Fact]
    public void NewGame_DealsFiveCardsToEachHand()
    {
        var state = NewRealGame();

        Assert.Equal(GameState.HandSize, state.HandOf(Side.White).Count);
        Assert.Equal(GameState.HandSize, state.HandOf(Side.Black).Count);
        Assert.Equal(state.HandOf(Side.White).Distinct().Count(), state.HandOf(Side.White).Count);
    }

    [Fact]
    public void NewGame_NeverDealsACardForAPieceThatCantMoveYet()
    {
        var state = NewRealGame(whiteOrder: [CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Ace, .. Deck.AllRanks]);

        Assert.DoesNotContain(CardRank.King, state.HandOf(Side.White));
        Assert.DoesNotContain(CardRank.Queen, state.HandOf(Side.White));
        Assert.DoesNotContain(CardRank.Jack, state.HandOf(Side.White));
        Assert.DoesNotContain(CardRank.Ace, state.HandOf(Side.White));
        Assert.All(state.HandOf(Side.White), c => Assert.True(c.ToPieceKind() is PieceKind.Pawn or PieceKind.Knight));
    }

    [Fact]
    public void NewGame_NeverDealsACardForAPieceThatNoLongerExists()
    {
        var engine = GeraChessRulesEngine.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNB1KBNR w KQkq - 0 1");
        var deck = new Deck([CardRank.Queen, .. Deck.AllRanks.Where(r => r != CardRank.Queen)]);
        var state = new GameState(engine, new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero), deck, Deck.Shuffled());

        Assert.DoesNotContain(CardRank.Queen, state.HandOf(Side.White));
    }

    [Fact]
    public void MakeMove_RefillNeverDealsACardForAPieceThatNoLongerExists()
    {
        var engine = GeraChessRulesEngine.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNB1KBNR w KQkq - 0 1");
        var deck = new Deck([
            CardRank.Ten, CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Five,
            CardRank.Queen, .. Deck.AllRanks.Where(r => r != CardRank.Queen),
        ]);
        var state = new GameState(engine, new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero), deck, Deck.Shuffled());
        Assert.Contains(CardRank.Ten, state.HandOf(Side.White));

        state.MakeMove(Square.Parse("b1"), Square.Parse("c3"), null, TimeSpan.FromSeconds(1));

        Assert.DoesNotContain(CardRank.Queen, state.HandOf(Side.White));
    }

    [Fact]
    public void NewGame_BothSidesStartAt3Hp_NotGameOver()
    {
        var state = NewRealGame();

        Assert.Equal(GameState.StartingHp, state.HpOf(Side.White));
        Assert.Equal(GameState.StartingHp, state.HpOf(Side.Black));
        Assert.False(state.IsGameOver);
        Assert.False(state.EmergencyMoveAvailable);
    }

    [Fact]
    public void AvailableMoves_AtStart_IsTheUnionOfEveryHandCardsLegalMoves()
    {
        var state = NewRealGame(whiteOrder: [CardRank.Six, .. Deck.AllRanks]);

        Assert.Contains(state.AvailableMoves, m => m.From == Square.Parse("e2") && m.To == Square.Parse("e4"));
    }

    [Fact]
    public void MakeMove_NotAmongAvailableMoves_Throws()
    {
        var state = NewRealGame();

        Assert.Throws<InvalidOperationException>(
            () => state.MakeMove(Square.Parse("a1"), Square.Parse("a8"), null, TimeSpan.Zero));
    }

    [Fact]
    public void MakeMove_UsingAHandCard_ConsumesItAndDrawsAReplacement_KeepingHandSizeConstant()
    {
        var state = NewRealGame(whiteOrder: [CardRank.Six, .. Deck.AllRanks.Where(r => r != CardRank.Six)]);
        Assert.Contains(CardRank.Six, state.HandOf(Side.White));

        state.MakeMove(Square.Parse("e2"), Square.Parse("e4"), null, TimeSpan.FromSeconds(1));

        Assert.DoesNotContain(CardRank.Six, state.HandOf(Side.White));
        Assert.Equal(GameState.HandSize, state.HandOf(Side.White).Count);
    }

    [Fact]
    public void MakeMove_CompletesTurn_SwitchesSideAndRecordsHistory()
    {
        var state = NewRealGame(whiteOrder: [CardRank.Six, .. Deck.AllRanks]);

        state.MakeMove(Square.Parse("e2"), Square.Parse("e4"), null, TimeSpan.FromSeconds(1));

        Assert.Equal(Side.Black, state.SideToMove);
        Assert.Single(state.MoveHistory);
    }

    [Fact]
    public void MakeMove_DeductsElapsedAndAppliesIncrement()
    {
        var state = new GameState(new GeraChessRulesEngine(), new Clock(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(3)),
            new Deck([CardRank.Six, .. Deck.AllRanks]), new Deck(Deck.AllRanks));

        state.MakeMove(Square.Parse("e2"), Square.Parse("e4"), null, TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(10) + TimeSpan.FromSeconds(3), state.Clock.WhiteRemaining);
    }

    [Fact]
    public void HandHasNoPlayableCard_WhenInCheck_OffersAnEmergencyMoveOverAllLegalMoves()
    {
        var rescueMove = Move("e1", "f2", PieceKind.King);
        var (state, engine) = NewFakeGame(
            [CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Ace, CardRank.Ten],
            e => { e.AllMoves = [rescueMove]; e.InCheck = true; });

        Assert.True(state.HandHasNoPlayableCard);
        Assert.True(state.EmergencyMoveAvailable);
        Assert.Equal([rescueMove], state.AvailableMoves);
        Assert.Equal(GameState.StartingHp, state.HpOf(Side.White));
    }

    [Fact]
    public void MakeMove_DuringAnEmergency_CostsExactlyOneHp_AndDoesNotTouchTheHand()
    {
        var rescueMove = Move("e1", "f2", PieceKind.King);
        var (state, engine) = NewFakeGame(
            [CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Ace, CardRank.Ten],
            e => { e.AllMoves = [rescueMove]; e.InCheck = true; });
        var handBefore = state.HandOf(Side.White).ToArray();

        state.MakeMove(rescueMove.From, rescueMove.To, null, TimeSpan.Zero);

        Assert.Equal(GameState.StartingHp - 1, state.HpOf(Side.White));
        Assert.Equal(handBefore, state.HandOf(Side.White));
    }

    [Fact]
    public void EmergencyMove_NeededAgainAfterHpReachesZero_EndsTheGameImmediately()
    {
        var rescueMove = Move("e1", "f2", PieceKind.King);
        var (state, engine) = NewFakeGame(
            [CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Ace, CardRank.Ten],
            e => { e.AllMoves = [rescueMove]; e.InCheck = true; });

        for (var i = 0; i < GameState.StartingHp; i++)
        {
            Assert.Equal(Side.White, state.SideToMove);
            Assert.False(state.IsGameOver);
            state.MakeMove(rescueMove.From, rescueMove.To, null, TimeSpan.Zero);

            Assert.Equal(Side.Black, state.SideToMove);
            state.MakeMove(rescueMove.From, rescueMove.To, null, TimeSpan.Zero);
        }

        Assert.Equal(0, state.HpOf(Side.White));
        Assert.Equal(Side.White, state.SideToMove);
        Assert.True(state.IsGameOver);
        Assert.False(state.EmergencyMoveAvailable);
        Assert.Equal(GameEndReason.HpDepleted, state.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, state.EndResult.Value.Winner);
        Assert.Empty(state.AvailableMoves);
        Assert.Throws<InvalidOperationException>(() => state.MakeMove(rescueMove.From, rescueMove.To, null, TimeSpan.Zero));
    }

    [Fact]
    public void HandHasNoPlayableCard_WhenNotInCheck_FallsBackToAnyMove_ForFree()
    {
        var freeMove = Move("d1", "d4", PieceKind.Queen);
        var (state, engine) = NewFakeGame(
            [CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Ace, CardRank.Ten],
            e => { e.AllMoves = [freeMove]; e.InCheck = false; });

        Assert.True(state.HandHasNoPlayableCard);
        Assert.False(state.EmergencyMoveAvailable);
        Assert.Equal([freeMove], state.AvailableMoves);

        var handBefore = state.HandOf(Side.White).ToArray();
        state.MakeMove(freeMove.From, freeMove.To, null, TimeSpan.Zero);

        Assert.Equal(GameState.StartingHp, state.HpOf(Side.White));
        Assert.Equal(handBefore, state.HandOf(Side.White));
    }

    [Fact]
    public void Checkmate_DetectedByTheEngine_EndsTheGameBeforeAnyEmergencyLogicRuns()
    {
        var (state, engine) = NewFakeGame();
        engine.EndResult = new GameEndResult(GameEndReason.Checkmate, Side.Black);

        Assert.True(state.IsGameOver);
        Assert.Empty(state.AvailableMoves);
        Assert.Throws<InvalidOperationException>(
            () => state.MakeMove(Square.Parse("a1"), Square.Parse("a2"), null, TimeSpan.Zero));
    }

    [Fact]
    public void Resign_EndsGameWithOpponentAsWinner()
    {
        var (state, engine) = NewFakeGame();

        state.Resign(Side.White);

        Assert.Equal(Side.White, engine.Resigned);
    }

    [Fact]
    public void DeclareTimeoutIfFlagged_WhenClockExpired_EndsGame()
    {
        var state = new GameState(new GeraChessRulesEngine(), new Clock(TimeSpan.Zero, TimeSpan.Zero),
            new Deck(Deck.AllRanks), new Deck(Deck.AllRanks));

        state.DeclareTimeoutIfFlagged();

        Assert.True(state.IsGameOver);
        Assert.Equal(GameEndReason.Timeout, state.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, state.EndResult.Value.Winner);
    }

    [Fact]
    public void PawnCard_CanMoveAnyPawnWithALegalMove_NotJustOneTiedToAFile()
    {
        var state = NewRealGame(whiteOrder: [CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six]);

        var moves = state.AvailableMoves;
        Assert.Contains(moves, m => m.From == Square.Parse("a2"));
        Assert.Contains(moves, m => m.From == Square.Parse("g2"));
        Assert.Contains(moves, m => m.From == Square.Parse("h2"));
    }

    [Fact]
    public void SelectCardsForReroll_MoreThanMax_Throws()
    {
        var state = NewRealGame();

        Assert.Throws<ArgumentException>(
            () => state.SelectCardsForReroll([CardRank.Two, CardRank.Three, CardRank.Four]));
    }

    [Fact]
    public void SelectCardsForReroll_DuplicateCard_Throws()
    {
        var state = NewRealGame();

        Assert.Throws<ArgumentException>(() => state.SelectCardsForReroll([CardRank.Two, CardRank.Two]));
    }

    [Fact]
    public void SelectCardsForReroll_CardNotInHand_Throws()
    {
        var state = NewRealGame();

        Assert.Throws<ArgumentException>(() => state.SelectCardsForReroll([CardRank.King]));
    }

    [Fact]
    public void SelectCardsForReroll_AfterGameOver_Throws()
    {
        var (state, engine) = NewFakeGame();
        engine.EndResult = new GameEndResult(GameEndReason.Checkmate, Side.Black);

        Assert.Throws<InvalidOperationException>(() => state.SelectCardsForReroll([]));
    }

    [Fact]
    public void SelectCardsForReroll_MarksCardsWithoutTouchingHand()
    {
        var state = NewRealGame();
        var handBefore = state.HandOf(Side.White).ToArray();
        var movesBefore = state.AvailableMoves;

        state.SelectCardsForReroll([CardRank.Two, CardRank.Three]);

        Assert.Equal([CardRank.Two, CardRank.Three], state.PendingRerollOf(Side.White));
        Assert.Equal(handBefore, state.HandOf(Side.White));
        Assert.Equal(movesBefore, state.AvailableMoves);
    }

    [Fact]
    public void SelectCardsForReroll_CalledAgain_ReplacesTheEarlierSelectionRatherThanAddingToIt()
    {
        var state = NewRealGame();

        state.SelectCardsForReroll([CardRank.Two, CardRank.Three]);
        state.SelectCardsForReroll([CardRank.Four]);

        Assert.Equal([CardRank.Four], state.PendingRerollOf(Side.White));
    }

    [Fact]
    public void MarkedCards_AreSwappedForFreshOnesRightAsItsThatSidesTurnAgain()
    {
        var state = NewRealGame(
            whiteOrder: [CardRank.Six, .. Deck.AllRanks.Where(r => r != CardRank.Six)],
            blackOrder: Deck.AllRanks);

        state.SelectCardsForReroll([CardRank.Two, CardRank.Three]);
        PlayTurn(state, "e2", "e4");
        PlayTurn(state, "e7", "e5");

        Assert.Empty(state.PendingRerollOf(Side.White));
        Assert.Equal(GameState.HandSize, state.HandOf(Side.White).Count);
        Assert.DoesNotContain(CardRank.Two, state.HandOf(Side.White));
        Assert.DoesNotContain(CardRank.Three, state.HandOf(Side.White));
        Assert.Contains(CardRank.Four, state.HandOf(Side.White));
        Assert.Contains(CardRank.Five, state.HandOf(Side.White));
    }

    [Fact]
    public void MoveMatchingBothMarkedAndUnmarkedCards_SpendsTheUnmarkedCardAndRerollsBothMarkedOnes()
    {
        var state = NewRealGame(
            whiteOrder: [CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Ten, CardRank.Jack, CardRank.Five, CardRank.Six, CardRank.Seven, CardRank.Eight, CardRank.Nine, CardRank.Queen, CardRank.King, CardRank.Ace],
            blackOrder: Deck.AllRanks);

        state.SelectCardsForReroll([CardRank.Two, CardRank.Three]);
        PlayTurn(state, "e2", "e4");

        Assert.DoesNotContain(CardRank.Four, state.HandOf(Side.White));
        Assert.Contains(CardRank.Two, state.HandOf(Side.White));
        Assert.Contains(CardRank.Three, state.HandOf(Side.White));
        Assert.Equal([CardRank.Two, CardRank.Three], state.PendingRerollOf(Side.White));

        PlayTurn(state, "e7", "e5");

        Assert.Empty(state.PendingRerollOf(Side.White));
        Assert.Equal(GameState.HandSize, state.HandOf(Side.White).Count);
        Assert.DoesNotContain(CardRank.Two, state.HandOf(Side.White));
        Assert.DoesNotContain(CardRank.Three, state.HandOf(Side.White));
    }

    [Fact]
    public void MarkingEveryCardOfAPieceKind_RemovesThatPiecesMovesThisTurn()
    {
        var pawnMove = Move("e2", "e4", PieceKind.Pawn);
        var knightMove = Move("g1", "f3", PieceKind.Knight);
        var (state, _) = NewFakeGame(
            [CardRank.Two, CardRank.Three, CardRank.Ten, CardRank.King, CardRank.Queen],
            e =>
            {
                e.AlwaysHasLegalMove = true;
                e.InCheck = false;
                e.AllMoves = [pawnMove, knightMove];
                e.MovesByKind[PieceKind.Pawn] = [pawnMove];
                e.MovesByKind[PieceKind.Knight] = [knightMove];
            });

        state.SelectCardsForReroll([CardRank.Two, CardRank.Three]);

        Assert.Equal([knightMove], state.AvailableMoves);
        Assert.Throws<InvalidOperationException>(() =>
            state.MakeMove(pawnMove.From, pawnMove.To, null, TimeSpan.Zero));

        state.SelectCardsForReroll([CardRank.Two]);

        Assert.Contains(pawnMove, state.AvailableMoves);
    }

    [Fact]
    public void MarkingEveryCardThatCouldMove_LeavesNoMovesInsteadOfGrantingAFreeOne()
    {
        var pawnMove = Move("e2", "e4", PieceKind.Pawn);
        var queenMove = Move("d1", "h5", PieceKind.Queen);
        var (state, _) = NewFakeGame(
            [CardRank.Two, CardRank.Three, CardRank.Ten, CardRank.King, CardRank.Jack],
            e =>
            {
                e.AlwaysHasLegalMove = true;
                e.InCheck = false;
                e.AllMoves = [pawnMove, queenMove];
                e.MovesByKind[PieceKind.Pawn] = [pawnMove];
            });

        state.SelectCardsForReroll([CardRank.Two, CardRank.Three]);

        Assert.Empty(state.AvailableMoves);
        Assert.False(state.HandHasNoPlayableCard);
        Assert.False(state.EmergencyMoveAvailable);
    }

    [Fact]
    public void Reroll_NeverProducesADuplicateRankInHand_EvenAfterTheDeckCyclesRepeatedly()
    {
        var (state, _) = NewFakeGame(configure: engine =>
        {
            engine.AlwaysHasLegalMove = true;
            foreach (var kind in new[] { PieceKind.Pawn, PieceKind.Knight, PieceKind.Bishop, PieceKind.Rook, PieceKind.Queen, PieceKind.King })
                engine.MovesByKind[kind] = [Move("a1", "a2", kind)];
        });

        for (var i = 0; i < 30; i++)
        {
            var hand = state.HandOf(state.SideToMove);
            state.SelectCardsForReroll([.. hand.Take(GameState.MaxRerollSelection)]);

            var move = state.AvailableMoves.First();
            state.MakeMove(move.From, move.To, move.PromoteTo, TimeSpan.FromSeconds(1));

            Assert.Equal(state.HandOf(Side.White).Distinct().Count(), state.HandOf(Side.White).Count);
            Assert.Equal(state.HandOf(Side.Black).Distinct().Count(), state.HandOf(Side.Black).Count);
        }
    }

    private static void PlayTurn(GameState state, string from, string to)
    {
        var move = state.AvailableMoves.Single(m => m.From == Square.Parse(from) && m.To == Square.Parse(to));
        state.MakeMove(move.From, move.To, move.PromoteTo, TimeSpan.FromSeconds(1));
    }

    // Regression: a simulation of many full Arcane Chess games (GameSimulationTests) found that
    // Gera.Chess can occasionally still offer a move that captures the opponent's king outright
    // instead of having already ended the game by checkmate — reproducible after Arcane Chess's
    // MindSwap relocates a king to a square the engine's incremental check tracking doesn't follow
    // reliably. A king must never actually be captured, so such moves are filtered out of
    // AvailableMoves regardless of source, and if that was the only "legal" move left, the game is
    // declared over by checkmate in the mover's favor instead of leaving them stuck.
    [Fact]
    public void EveryEngineLegalMove_WouldCaptureTheKing_DeclaresCheckmateForTheMoverInstead()
    {
        var kingCapture = Move("e4", "e8", PieceKind.Rook, captured: PieceKind.King);
        var (state, _) = NewFakeGame(configure: engine =>
        {
            engine.AllMoves = [kingCapture];
            engine.InCheck = false;
        });

        Assert.Empty(state.AvailableMoves);
        Assert.True(state.IsGameOver);
        Assert.Equal(GameEndReason.Checkmate, state.EndResult!.Value.Reason);
        Assert.Equal(Side.White, state.EndResult.Value.Winner);
    }
}
