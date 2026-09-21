namespace ChessLab.Core.Chess;

/// <summary>Textual edits to a FEN position's piece placement — the only way to put a piece
/// somewhere the normal move rules would never allow (teleport, swap, forced removal). Used
/// exclusively by Arcane Chess spells; regular chess moves go through <see cref="IChessRulesEngine.ApplyMove"/>.</summary>
public static class FenBoard
{
    public static char? PieceAt(string fen, Square square) => ParsePlacement(fen)[square.File, square.Rank];

    public static bool HasPiece(string fen, Side side, PieceKind kind) =>
        fen.Split(' ')[0].Contains(PieceSymbol(side, kind));

    private static char PieceSymbol(Side side, PieceKind kind)
    {
        var symbol = kind switch
        {
            PieceKind.Pawn => 'p',
            PieceKind.Knight => 'n',
            PieceKind.Bishop => 'b',
            PieceKind.Rook => 'r',
            PieceKind.Queen => 'q',
            PieceKind.King => 'k',
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return side == Side.White ? char.ToUpperInvariant(symbol) : symbol;
    }

    /// <summary>Forces whose turn it is without moving any piece, clearing the en-passant target
    /// (which wouldn't apply to a hypothetical turn). Used by the Arcane Chess "Extra Turn" spell.</summary>
    public static string WithSideToMove(string fen, Side side)
    {
        var fields = fen.Split(' ');
        fields[1] = side == Side.White ? "w" : "b";
        fields[3] = "-";
        return string.Join(' ', fields);
    }

    /// <summary>Moves the piece at <paramref name="from"/> to <paramref name="to"/> (which must be
    /// empty), or removes it entirely when <paramref name="to"/> is null. Clears the en-passant
    /// target and revokes castling rights tied to any home square this edit touches.</summary>
    public static string MovePiece(string fen, Square from, Square? to)
    {
        var grid = ParsePlacement(fen);
        var piece = grid[from.File, from.Rank] ?? throw new ArgumentException($"No piece at {from}.", nameof(from));

        grid[from.File, from.Rank] = null;
        if (to is { } target)
        {
            if (grid[target.File, target.Rank] is not null)
                throw new ArgumentException($"{target} is not empty.", nameof(to));

            grid[target.File, target.Rank] = piece;
        }

        Square[] touched = to is { } t ? [from, t] : [from];
        return Rebuild(fen, grid, touched);
    }

    /// <summary>Swaps the pieces occupying <paramref name="a"/> and <paramref name="b"/> — both must
    /// be occupied.</summary>
    public static string SwapPieces(string fen, Square a, Square b)
    {
        var grid = ParsePlacement(fen);
        var pieceA = grid[a.File, a.Rank] ?? throw new ArgumentException($"No piece at {a}.", nameof(a));
        var pieceB = grid[b.File, b.Rank] ?? throw new ArgumentException($"No piece at {b}.", nameof(b));

        grid[a.File, a.Rank] = pieceB;
        grid[b.File, b.Rank] = pieceA;

        return Rebuild(fen, grid, [a, b]);
    }

    private static char?[,] ParsePlacement(string fen)
    {
        var grid = new char?[8, 8];
        var placement = fen.Split(' ')[0];
        var ranks = placement.Split('/');
        if (ranks.Length != 8)
            throw new FormatException($"Invalid FEN placement: '{placement}'.");

        for (var rankIndexFromTop = 0; rankIndexFromTop < 8; rankIndexFromTop++)
        {
            var rank = 7 - rankIndexFromTop;
            var file = 0;
            foreach (var c in ranks[rankIndexFromTop])
            {
                if (char.IsDigit(c))
                {
                    file += c - '0';
                    continue;
                }

                grid[file, rank] = c;
                file++;
            }

            if (file != 8)
                throw new FormatException($"Invalid FEN rank: '{ranks[rankIndexFromTop]}'.");
        }

        return grid;
    }

    private static string Rebuild(string fen, char?[,] grid, IReadOnlyList<Square> touchedSquares)
    {
        var fields = fen.Split(' ');
        fields[0] = ToPlacement(grid);
        fields[3] = "-"; // en passant target no longer applies after an out-of-band board edit
        fields[2] = RevokeCastlingRights(fields[2], touchedSquares);
        return string.Join(' ', fields);
    }

    private static string ToPlacement(char?[,] grid)
    {
        var ranks = new string[8];
        for (var rank = 7; rank >= 0; rank--)
        {
            var sb = new System.Text.StringBuilder();
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                var piece = grid[file, rank];
                if (piece is null)
                {
                    empty++;
                    continue;
                }

                if (empty > 0)
                {
                    sb.Append(empty);
                    empty = 0;
                }

                sb.Append(piece.Value);
            }

            if (empty > 0)
                sb.Append(empty);

            ranks[7 - rank] = sb.ToString();
        }

        return string.Join('/', ranks);
    }

    private static readonly Dictionary<Square, string> CastlingRightsBySquare = new()
    {
        [new Square(4, 0)] = "KQ", // e1 — white king home
        [new Square(0, 0)] = "Q",  // a1 — white queenside rook home
        [new Square(7, 0)] = "K",  // h1 — white kingside rook home
        [new Square(4, 7)] = "kq", // e8 — black king home
        [new Square(0, 7)] = "q",  // a8 — black queenside rook home
        [new Square(7, 7)] = "k",  // h8 — black kingside rook home
    };

    private static string RevokeCastlingRights(string castling, IReadOnlyList<Square> touchedSquares)
    {
        if (castling == "-")
            return castling;

        var revoked = touchedSquares
            .Where(CastlingRightsBySquare.ContainsKey)
            .SelectMany(s => CastlingRightsBySquare[s]);

        var remaining = new string(castling.Where(c => !revoked.Contains(c)).ToArray());
        return remaining.Length == 0 ? "-" : remaining;
    }
}
