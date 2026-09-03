using ChessLab.Core.Chess;

namespace ChessLab.Core.Tests.Chess;

public class GeraChessRulesEngineTests
{
    private static ChessMove Find(IReadOnlyList<ChessMove> moves, string from, string to, PieceKind? promoteTo = null) =>
        moves.Single(m => m.From == Square.Parse(from) && m.To == Square.Parse(to) && m.PromoteTo == promoteTo);

    [Fact]
    public void NewGame_WhiteToMove()
    {
        var engine = new GeraChessRulesEngine();
        Assert.Equal(Side.White, engine.SideToMove);
        Assert.Null(engine.EndResult);
    }

    [Fact]
    public void NewGame_Has20LegalMoves()
    {
        var engine = new GeraChessRulesEngine();
        Assert.Equal(20, engine.LegalMoves().Count);
    }

    [Fact]
    public void LegalMoves_FilteredByKind_OnlyContainsThatKind()
    {
        var engine = new GeraChessRulesEngine();
        var knightMoves = engine.LegalMoves(PieceKind.Knight);

        Assert.Equal(4, knightMoves.Count);
        Assert.All(knightMoves, m => Assert.Equal(PieceKind.Knight, m.Piece));
    }

    [Fact]
    public void ApplyMove_SwitchesSideToMove()
    {
        var engine = new GeraChessRulesEngine();
        var move = Find(engine.LegalMoves(), "e2", "e4");

        engine.ApplyMove(move);

        Assert.Equal(Side.Black, engine.SideToMove);
    }

    [Fact]
    public void ApplyMove_NotInLegalMoves_Throws()
    {
        var engine = new GeraChessRulesEngine();
        var illegal = new ChessMove(Square.Parse("e2"), Square.Parse("e5"), PieceKind.Pawn, null, null, false, false, "");

        Assert.Throws<InvalidOperationException>(() => engine.ApplyMove(illegal));
    }

    [Fact]
    public void FoolsMate_EndsInCheckmate_BlackWins()
    {
        var engine = new GeraChessRulesEngine();

        engine.ApplyMove(Find(engine.LegalMoves(), "f2", "f3"));
        engine.ApplyMove(Find(engine.LegalMoves(), "e7", "e5"));
        engine.ApplyMove(Find(engine.LegalMoves(), "g2", "g4"));
        engine.ApplyMove(Find(engine.LegalMoves(), "d8", "h4"));

        Assert.NotNull(engine.EndResult);
        Assert.Equal(GameEndReason.Checkmate, engine.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, engine.EndResult!.Value.Winner);
    }

    [Fact]
    public void KnownStalematePosition_IsDetected()
    {
        var engine = GeraChessRulesEngine.FromFen("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");

        Assert.NotNull(engine.EndResult);
        Assert.Equal(GameEndReason.Stalemate, engine.EndResult!.Value.Reason);
        Assert.Null(engine.EndResult!.Value.Winner);
    }

    [Fact]
    public void PawnPromotion_OffersAllFourPieceKinds()
    {
        var engine = GeraChessRulesEngine.FromFen("8/P7/8/8/8/2k5/8/6K1 w - - 0 1");
        var pawnMoves = engine.LegalMoves(PieceKind.Pawn);

        var promotions = pawnMoves.Where(m => m.From == Square.Parse("a7") && m.To == Square.Parse("a8")).ToArray();

        Assert.Equal(4, promotions.Length);
        Assert.Contains(promotions, m => m.PromoteTo == PieceKind.Queen);
        Assert.Contains(promotions, m => m.PromoteTo == PieceKind.Rook);
        Assert.Contains(promotions, m => m.PromoteTo == PieceKind.Bishop);
        Assert.Contains(promotions, m => m.PromoteTo == PieceKind.Knight);
    }

    [Fact]
    public void PawnPromotion_ToQueen_ReflectedInFen()
    {
        var engine = GeraChessRulesEngine.FromFen("8/P7/8/8/8/2k5/8/6K1 w - - 0 1");
        var promoteToQueen = Find(engine.LegalMoves(PieceKind.Pawn), "a7", "a8", PieceKind.Queen);

        engine.ApplyMove(promoteToQueen);

        Assert.StartsWith("Q7/", engine.ToFen());
    }

    [Fact]
    public void CastlingKingside_MovesRookToo()
    {
        var engine = GeraChessRulesEngine.FromFen("r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1");
        var castle = Find(engine.LegalMoves(PieceKind.King), "e1", "g1");

        engine.ApplyMove(castle);

        Assert.Contains("R4RK1", engine.ToFen());
    }

    [Fact]
    public void EnPassant_CapturesThePassingPawn()
    {
        var engine = GeraChessRulesEngine.FromFen("4k3/8/8/8/3pP3/8/8/4K3 b - e3 0 1");
        var enPassant = Find(engine.LegalMoves(PieceKind.Pawn), "d4", "e3");

        Assert.Equal(PieceKind.Pawn, enPassant.CapturedPiece);

        engine.ApplyMove(enPassant);

        Assert.Equal(Side.White, engine.SideToMove);
    }

    [Fact]
    public void HasLegalMove_ForTheSideToMove_MatchesLegalMoves()
    {
        var engine = new GeraChessRulesEngine();

        Assert.True(engine.HasLegalMove(Side.White, PieceKind.Pawn));
        Assert.True(engine.HasLegalMove(Side.White, PieceKind.Knight));
        Assert.False(engine.HasLegalMove(Side.White, PieceKind.King)); // boxed in at the start
        Assert.False(engine.HasLegalMove(Side.White, PieceKind.Queen));
    }

    [Fact]
    public void HasLegalMove_ForTheOtherSide_StillWorksEvenWhileTheSideToMoveIsInCheck()
    {
        // White to move, in check — probing Black (the side not to move) must not blow up just
        // because the position looks "illegal" from Black's hypothetical point of view.
        var engine = GeraChessRulesEngine.FromFen("4qk2/8/8/8/8/8/8/4K3 w - - 0 1");

        Assert.True(engine.IsInCheck(Side.White));
        Assert.True(engine.HasLegalMove(Side.Black, PieceKind.Queen));
        Assert.True(engine.HasLegalMove(Side.Black, PieceKind.King));
        Assert.False(engine.HasLegalMove(Side.Black, PieceKind.Rook)); // Black has no rook here
    }

    [Fact]
    public void HasLegalMove_IsFalse_WhenThatPieceKindNoLongerExistsForThatSide()
    {
        var engine = GeraChessRulesEngine.FromFen("4k3/8/8/8/8/8/8/4K3 w - - 0 1"); // no queens on the board

        Assert.False(engine.HasLegalMove(Side.White, PieceKind.Queen));
        Assert.False(engine.HasLegalMove(Side.Black, PieceKind.Queen));
    }

    [Fact]
    public void LegalMoves_ReflectsPositionAfterLoadPosition_NotAStalePriorCache()
    {
        var engine = new GeraChessRulesEngine();
        _ = engine.LegalMoves();

        engine.LoadPosition("7k/5Q2/6K1/8/8/8/8/8 b - - 0 1");

        Assert.Empty(engine.LegalMoves());
    }

    [Fact]
    public void HasLegalMove_ForOtherSide_ReflectsChangedPosition_NotAStalePriorProbe()
    {
        var engine = GeraChessRulesEngine.FromFen("4k3/8/8/8/8/8/8/4K3 w - - 0 1");
        Assert.False(engine.HasLegalMove(Side.Black, PieceKind.Queen));

        engine.LoadPosition("4qk2/8/8/8/8/8/8/4K3 w - - 0 1");

        Assert.True(engine.HasLegalMove(Side.Black, PieceKind.Queen));
    }

    [Fact]
    public void Resign_CachedLegalMovesQueriedBeforehand_DoesNotSuppressEndResult()
    {
        var engine = new GeraChessRulesEngine();
        _ = engine.LegalMoves();

        engine.Resign(Side.White);

        Assert.NotNull(engine.EndResult);
        Assert.Equal(GameEndReason.Resignation, engine.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, engine.EndResult!.Value.Winner);
    }

    [Fact]
    public void DeclareTimeout_CachedLegalMovesQueriedBeforehand_DoesNotSuppressEndResult()
    {
        var engine = new GeraChessRulesEngine();
        _ = engine.LegalMoves();

        engine.DeclareTimeout(Side.White);

        Assert.NotNull(engine.EndResult);
        Assert.Equal(GameEndReason.Timeout, engine.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, engine.EndResult!.Value.Winner);
    }
}
