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

    /// <summary>The engine must be fully configured *before* GameState is constructed — the
    /// constructor computes the opening AvailableMoves immediately, so anything set afterward would
    /// be too late to matter for that first evaluation.</summary>
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
        // At the very start only pawns and knights can move — every other piece is boxed in, so
        // stacking non-pawn/knight cards first must get skipped rather than dealt.
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
        // No white queen anywhere on the board — "Queen" stacked first in the draw order must
        // never end up in White's hand, since it could never resolve to a move.
        var engine = GeraChessRulesEngine.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNB1KBNR w KQkq - 0 1");
        var deck = new Deck([CardRank.Queen, .. Deck.AllRanks.Where(r => r != CardRank.Queen)]);
        var state = new GameState(engine, new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero), deck, Deck.Shuffled());

        Assert.DoesNotContain(CardRank.Queen, state.HandOf(Side.White));
    }

    [Fact]
    public void MakeMove_RefillNeverDealsACardForAPieceThatNoLongerExists()
    {
        var engine = GeraChessRulesEngine.FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNB1KBNR w KQkq - 0 1");
        // The initial deal must succeed without needing to skip anything (all pawn/knight cards),
        // then "Queen" is stacked right where the post-move refill draw would land.
        var deck = new Deck([
            CardRank.Ten, CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Five,
            CardRank.Queen, .. Deck.AllRanks.Where(r => r != CardRank.Queen),
        ]);
        var state = new GameState(engine, new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero), deck, Deck.Shuffled());
        Assert.Contains(CardRank.Ten, state.HandOf(Side.White));

        state.MakeMove(Square.Parse("b1"), Square.Parse("c3"), null, TimeSpan.FromSeconds(1)); // plays the "10" (knight) card

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
        // "6" = e-pawn, which always has a legal move from the starting position.
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
        // "Six" appears only once in this order — Deck.AllRanks already contains one, so this
        // excludes it from the tail to avoid a duplicate that would defeat the DoesNotContain check.
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
        // A hand of entirely non-pawn cards with no entries in MovesByKind — none of them can move.
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
        // Both sides happen to face the same "nothing in hand works, and it's check" situation via
        // the shared fake engine state — Black's side of it only matters here for alternating turns
        // realistically (a real engine switches SideToMove on every ApplyMove), not for its own sake.
        var rescueMove = Move("e1", "f2", PieceKind.King);
        var (state, engine) = NewFakeGame(
            [CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Ace, CardRank.Ten],
            e => { e.AllMoves = [rescueMove]; e.InCheck = true; });

        // 3 Emergency Moves bring White from 3 HP down to exactly 0 — reaching 0 doesn't itself end
        // the game, only needing *another* Emergency Move on White's next actual turn does (rule 8).
        for (var i = 0; i < GameState.StartingHp; i++)
        {
            Assert.Equal(Side.White, state.SideToMove);
            Assert.False(state.IsGameOver);
            state.MakeMove(rescueMove.From, rescueMove.To, null, TimeSpan.Zero); // White's turn

            Assert.Equal(Side.Black, state.SideToMove);
            state.MakeMove(rescueMove.From, rescueMove.To, null, TimeSpan.Zero); // Black's turn
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
        Assert.Equal(handBefore, state.HandOf(Side.White)); // untouched — this move wasn't a hand card
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

    /// <summary>
    /// A pawn card is generic — it lets you move any pawn with a legal move, exactly like a knight
    /// or bishop card lets you move either knight or either bishop. There's no per-file tracking of
    /// "which" pawn a card refers to: a hand built entirely from pawn ranks (2–9) offers moves for
    /// every pawn on the board, not just the ones whose file happens to match a card's rank.
    /// </summary>
    [Fact]
    public void PawnCard_CanMoveAnyPawnWithALegalMove_NotJustOneTiedToAFile()
    {
        var state = NewRealGame(whiteOrder: [CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six]);

        var moves = state.AvailableMoves;
        Assert.Contains(moves, m => m.From == Square.Parse("a2")); // matches a card's rank (file a)
        Assert.Contains(moves, m => m.From == Square.Parse("g2")); // no card's rank maps to file g
        Assert.Contains(moves, m => m.From == Square.Parse("h2")); // no card's rank maps to file h
    }

    private static void PlayTurn(GameState state, string from, string to)
    {
        var move = state.AvailableMoves.Single(m => m.From == Square.Parse(from) && m.To == Square.Parse(to));
        state.MakeMove(move.From, move.To, move.PromoteTo, TimeSpan.FromSeconds(1));
    }
}
