using BrainAndHand.Core.Chess;
using BrainAndHand.Core.HandBrain;

namespace BrainAndHand.Core.Tests.HandBrain;

public class GameStateTests
{
    private static GameState NewGame(TimeSpan? initial = null, TimeSpan? increment = null) =>
        new(new GeraChessRulesEngine(), new Clock(initial ?? TimeSpan.FromMinutes(10), increment ?? TimeSpan.FromSeconds(5)));

    [Fact]
    public void Initially_BrainSelectingAndWhiteToMove()
    {
        var state = NewGame();

        Assert.Equal(TurnPhase.BrainSelecting, state.Phase);
        Assert.Equal(Side.White, state.SideToMove);
        Assert.False(state.IsGameOver);
    }

    [Fact]
    public void AvailablePieceKinds_AtStart_OnlyPawnAndKnight()
    {
        var state = NewGame();

        var kinds = state.AvailablePieceKinds();

        Assert.Equal([PieceKind.Pawn, PieceKind.Knight], kinds.OrderBy(k => k));
    }

    [Fact]
    public void SelectPieceKind_WithoutLegalMove_Throws()
    {
        var state = NewGame();

        Assert.Throws<InvalidOperationException>(() => state.SelectPieceKind(PieceKind.Queen));
    }

    [Fact]
    public void SelectPieceKind_TransitionsToHandMoving()
    {
        var state = NewGame();

        state.SelectPieceKind(PieceKind.Pawn);

        Assert.Equal(TurnPhase.HandMoving, state.Phase);
        Assert.Equal(PieceKind.Pawn, state.SelectedPieceKind);
    }

    [Fact]
    public void AvailableMoves_BeforePieceKindSelected_Throws()
    {
        var state = NewGame();

        Assert.Throws<InvalidOperationException>(() => state.AvailableMoves());
    }

    [Fact]
    public void MakeMove_CompletesTurn_SwitchesSideAndResetsPhase()
    {
        var state = NewGame();
        state.SelectPieceKind(PieceKind.Pawn);
        var move = state.AvailableMoves().Single(m => m.From == Square.Parse("e2") && m.To == Square.Parse("e4"));

        state.MakeMove(move.From, move.To, move.PromoteTo, TimeSpan.FromSeconds(10));

        Assert.Equal(TurnPhase.BrainSelecting, state.Phase);
        Assert.Null(state.SelectedPieceKind);
        Assert.Equal(Side.Black, state.SideToMove);
        Assert.Single(state.MoveHistory);
    }

    [Fact]
    public void MakeMove_NotMatchingAnnouncedKind_Throws()
    {
        var state = NewGame();
        state.SelectPieceKind(PieceKind.Knight);

        Assert.Throws<InvalidOperationException>(
            () => state.MakeMove(Square.Parse("e2"), Square.Parse("e4"), null, TimeSpan.Zero));
    }

    [Fact]
    public void Resign_EndsGameWithOpponentAsWinner()
    {
        var state = NewGame();

        state.Resign(Side.White);

        Assert.True(state.IsGameOver);
        Assert.Equal(GameEndReason.Resignation, state.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, state.EndResult!.Value.Winner);
    }

    [Fact]
    public void MakeMove_DeductsElapsedAndAppliesIncrement()
    {
        var state = NewGame(initial: TimeSpan.FromMinutes(5), increment: TimeSpan.FromSeconds(3));
        state.SelectPieceKind(PieceKind.Pawn);
        var move = state.AvailableMoves().Single(m => m.From == Square.Parse("e2") && m.To == Square.Parse("e4"));

        state.MakeMove(move.From, move.To, move.PromoteTo, TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromMinutes(5) - TimeSpan.FromSeconds(10) + TimeSpan.FromSeconds(3), state.Clock.WhiteRemaining);
    }

    [Fact]
    public void DeclareTimeoutIfFlagged_WhenClockExpired_EndsGame()
    {
        var state = NewGame(initial: TimeSpan.Zero);

        state.DeclareTimeoutIfFlagged();

        Assert.True(state.IsGameOver);
        Assert.Equal(GameEndReason.Timeout, state.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, state.EndResult!.Value.Winner);
    }

    [Fact]
    public void FoolsMate_PlayedThroughBrainAndHandFlow_EndsInCheckmate()
    {
        var state = NewGame();

        PlayTurn(state, PieceKind.Pawn, "f2", "f3");
        PlayTurn(state, PieceKind.Pawn, "e7", "e5");
        PlayTurn(state, PieceKind.Pawn, "g2", "g4");
        PlayTurn(state, PieceKind.Queen, "d8", "h4");

        Assert.True(state.IsGameOver);
        Assert.Equal(GameEndReason.Checkmate, state.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, state.EndResult!.Value.Winner);
    }

    [Fact]
    public void SelectPieceKind_AfterGameOver_Throws()
    {
        var state = NewGame();
        state.Resign(Side.White);

        Assert.Throws<InvalidOperationException>(() => state.SelectPieceKind(PieceKind.Pawn));
    }

    private static void PlayTurn(GameState state, PieceKind kind, string from, string to)
    {
        state.SelectPieceKind(kind);
        var move = state.AvailableMoves().Single(m => m.From == Square.Parse(from) && m.To == Square.Parse(to));
        state.MakeMove(move.From, move.To, move.PromoteTo, TimeSpan.FromSeconds(1));
    }
}
