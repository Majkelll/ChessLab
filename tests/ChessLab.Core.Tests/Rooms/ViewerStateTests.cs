using ChessLab.Core.Chess;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Rooms;

public class ViewerStateTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static Room FullRoom(GameKind kind)
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), kind);
        room.ClaimSeat(White, Guid.NewGuid(), "Ala");
        room.ClaimSeat(Black, Guid.NewGuid(), "Bob");
        return room;
    }

    private static ArcaneChessSession StartedArcaneGame()
    {
        var session = new ArcaneChessSession(FullRoom(GameKind.ArcaneChess));
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start, new Random(3));
        return session;
    }

    private static DraftChessSession DraftGameInPlacing()
    {
        var session = new DraftChessSession(FullRoom(GameKind.DraftChess));
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);
        session.Apply(GameAction.DraftPass(), White, Start);
        session.Apply(GameAction.DraftPass(), Black, Start);
        return session;
    }

    [Fact]
    public void ArcaneChess_HidesTheOpponentsHandsFromAPlayer()
    {
        var state = StartedArcaneGame().ToStateDto(Side.White);

        Assert.Equal(3, state.Arcane!.WhiteSpellHand.Count);
        Assert.Empty(state.Arcane.BlackSpellHand);
        Assert.Equal(5, state.CardChess!.WhiteHand.Count);
        Assert.Empty(state.CardChess.BlackHand);
    }

    [Fact]
    public void ArcaneChess_ShowsNoHandsToASpectator()
    {
        var state = StartedArcaneGame().ToStateDto(null);

        Assert.Empty(state.Arcane!.WhiteSpellHand);
        Assert.Empty(state.Arcane.BlackSpellHand);
        Assert.Empty(state.CardChess!.WhiteHand);
        Assert.Empty(state.CardChess.BlackHand);
    }

    [Fact]
    public void ArcaneChess_OffersAvailableMovesOnlyToTheSideToMove()
    {
        var session = StartedArcaneGame();

        Assert.NotEmpty(session.ToUpdateDto(Side.White).AvailableMoves);
        Assert.Empty(session.ToUpdateDto(Side.Black).AvailableMoves);
        Assert.Empty(session.ToUpdateDto(null).AvailableMoves);
    }

    [Fact]
    public void ArcaneChess_AfterTheGameEnds_ShowsEveryHandToEveryone()
    {
        var session = StartedArcaneGame();
        session.Game!.Resign(Side.White);

        var state = session.ToStateDto(null);

        Assert.Equal(3, state.Arcane!.BlackSpellHand.Count);
        Assert.Equal(5, state.CardChess!.BlackHand.Count);
    }

    [Fact]
    public void DraftChess_HidesTheOpponentsLayoutUntilPlayStarts()
    {
        var session = DraftGameInPlacing();
        session.Apply(GameAction.PlacePiece(PieceKind.King, Square.Parse("e1")), White, Start);

        Assert.Single(session.ToStateDto(Side.White).Draft!.WhitePlacements);
        Assert.Empty(session.ToStateDto(Side.Black).Draft!.WhitePlacements);
        Assert.Empty(session.ToStateDto(null).Draft!.WhitePlacements);

        session.Apply(GameAction.PlacePiece(PieceKind.King, Square.Parse("e8")), Black, Start);

        Assert.Equal(DraftPhase.Playing, session.Game!.Phase);
        Assert.Single(session.ToStateDto(Side.Black).Draft!.WhitePlacements);
    }

    [Fact]
    public void DraftChess_LetsASideThatFinishedPlacingTakeAPieceBack()
    {
        var session = DraftGameInPlacing();
        session.Apply(GameAction.PlacePiece(PieceKind.King, Square.Parse("e1")), White, Start);

        Assert.DoesNotContain(White, session.ActiveSeats);
        Assert.Contains(White, session.SeatsAllowedToAct);

        session.Apply(GameAction.UnplacePiece(Square.Parse("e1")), White, Start.AddSeconds(30));

        Assert.Contains(White, session.ActiveSeats);
        Assert.Equal(TimeSpan.FromMinutes(10), session.Game!.Clock.WhiteRemaining);
    }

    [Fact]
    public void DraftChess_ChargesEachSideOnlyItsOwnPlacingTime()
    {
        var session = new DraftChessSession(FullRoom(GameKind.DraftChess));
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);
        session.Apply(GameAction.DraftPick(PieceKind.Pawn), White, Start);
        session.Apply(GameAction.DraftPass(), Black, Start);
        session.Apply(GameAction.DraftPass(), White, Start);

        session.Apply(GameAction.PlacePiece(PieceKind.King, Square.Parse("e1")), White, Start.AddSeconds(5));
        session.Apply(GameAction.PlacePiece(PieceKind.King, Square.Parse("e8")), Black, Start.AddSeconds(8));

        Assert.Equal(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(5), session.Game!.Clock.WhiteRemaining);
        Assert.Equal(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(8), session.Game.Clock.BlackRemaining);
    }

    [Fact]
    public void ArcaneChess_PublishesWhetherTheSideToMoveHasCastThisTurn()
    {
        var session = StartedArcaneGame();
        var affordable = session.Game!.SpellHandOf(Side.White).FirstOrDefault(spell =>
            spell is ChessLab.Core.ArcaneChess.SpellRank.Peek or ChessLab.Core.ArcaneChess.SpellRank.Jam
                or ChessLab.Core.ArcaneChess.SpellRank.Feint, (ChessLab.Core.ArcaneChess.SpellRank)(-1));

        Assert.False(session.ToStateDto(Side.White).Arcane!.SideToMoveHasCast);
        if ((int)affordable < 0)
            return;

        session.Apply(GameAction.CastSpell(affordable, default), White, Start);

        Assert.True(session.ToStateDto(Side.White).Arcane!.SideToMoveHasCast);
    }

    [Fact]
    public void BiddingChess_WhenBothSidesRunOutOfTimeWhileBidding_WhiteLoses()
    {
        var session = new BiddingChessSession(FullRoom(GameKind.BiddingChess));
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);

        session.DeclareTimeoutIfExpired(Start.AddMinutes(11));

        Assert.Equal(new GameEndResult(GameEndReason.Timeout, Side.Black), session.Game!.EndResult);
    }
}
