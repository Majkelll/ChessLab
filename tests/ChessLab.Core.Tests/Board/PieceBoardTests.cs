using ChessLab.Core.Board;
using ChessLab.Core.Chess;

namespace ChessLab.Core.Tests.Board;

public class PieceBoardTests
{
    [Theory]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 1, 20)]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 2, 400)]
    [InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", 3, 8902)]
    [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 1, 48)]
    [InlineData("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", 2, 2039)]
    [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 1, 14)]
    [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 2, 191)]
    [InlineData("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", 3, 2812)]
    public void Perft_MatchesTheKnownNodeCounts(string fen, int depth, int expected) =>
        Assert.Equal(expected, Perft(PieceBoard.FromFen(fen), depth));

    [Fact]
    public void LegalMoves_ThroughRandomGames_AgreeWithTheChessLibraryEveryHalfmove()
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var rng = new Random(seed);
            var board = PieceBoard.StandardStart();
            var engine = new GeraChessRulesEngine();
            var log = new List<string>();

            for (var ply = 0; ply < 120 && engine.EndResult is null; ply++)
            {
                var ours = board.LegalMoves();
                var theirs = engine.LegalMoves();

                Assert.True(Signature(ours).SequenceEqual(Signature(theirs)),
                    $"Seed {seed}, ply {ply}, fen {board.ToFen()}\n" +
                    $"ours:   {string.Join(' ', Signature(ours))}\n" +
                    $"theirs: {string.Join(' ', Signature(theirs))}\n" +
                    $"moves:  {string.Join(' ', log)}");

                if (ours.Count == 0)
                    break;

                var move = ours[rng.Next(ours.Count)];
                var applied = board.Apply(move);
                engine.ApplyMove(move);
                log.Add(applied.San);

                Assert.Equal(engine.ToFen(), board.ToFen());
            }
        }
    }

    [Fact]
    public void LegalMoves_LeavingTheOwnKingAttacked_AreNotOffered()
    {
        var board = PieceBoard.FromFen("4k3/8/8/8/8/8/4R3/5K2 b - - 0 1");

        Assert.DoesNotContain(board.LegalMoves(), move => move.From == new Square(4, 7) && move.To == new Square(4, 6));
    }

    [Fact]
    public void PseudoLegalMoves_IgnoreCheckAndOfferTheKingCapture()
    {
        var board = PieceBoard.FromFen("4k3/8/8/8/8/8/8/4RK2 w - - 0 1");

        var moves = board.PseudoLegalMoves();

        Assert.Contains(moves, move => move.To == new Square(4, 7) && move.CapturedPiece == PieceKind.King);
    }

    [Fact]
    public void Apply_WithAbsorptionOn_GivesTheCapturingPieceTheCapturedPiecesPowers()
    {
        var board = PieceBoard.FromFen("4k3/8/8/8/8/8/3r4/2P1K3 w - - 0 1");
        board.AbsorbOnCapture = true;

        var capture = board.LegalMoves().Single(move => move.From == new Square(2, 0) && move.To == new Square(3, 1));
        board.Apply(capture);

        var piece = board.At(new Square(3, 1))!.Value;
        Assert.True(piece.Has(MovePower.Rook));
        Assert.True(piece.Has(MovePower.Pawn));
        Assert.Equal(PieceKind.Rook, piece.PrimaryKind);
    }

    [Fact]
    public void Apply_WithAbsorptionOff_LeavesTheCapturingPieceAsItWas()
    {
        var board = PieceBoard.FromFen("4k3/8/8/8/8/8/3r4/2P1K3 w - - 0 1");

        var capture = board.LegalMoves().Single(move => move.From == new Square(2, 0) && move.To == new Square(3, 1));
        board.Apply(capture);

        Assert.Equal(MovePower.Pawn, board.At(new Square(3, 1))!.Value.Powers);
    }

    private static IEnumerable<string> Signature(IReadOnlyList<ChessMove> moves) =>
        moves.Select(move => $"{move.From}{move.To}{(move.PromoteTo is { } p ? p.ToString()[0] : ' ')}")
            .OrderBy(text => text, StringComparer.Ordinal);

    private static int Perft(PieceBoard board, int depth)
    {
        if (depth == 0)
            return 1;

        var total = 0;
        foreach (var move in board.LegalMoves())
        {
            var next = board.Clone();
            next.Apply(move, annotate: false);
            total += Perft(next, depth - 1);
        }

        return total;
    }
}
