using System.Text;
using ChessLab.Core.Chess;

namespace ChessLab.Core.Board;

/// <summary>
/// A chess position ChessLab owns outright, for the modes the underlying chess library can't
/// express: Bidding Chess needs moves that ignore check and a king that can actually be captured,
/// Absorption Chess needs pieces whose powers stack, and Alice Chess needs legality decided across
/// two boards at once. Ordinary chess is the special case where every piece has exactly one power,
/// which is what the equivalence tests against the library check.
/// </summary>
public sealed class PieceBoard
{
    private static readonly (int File, int Rank)[] KnightDeltas =
        [(1, 2), (2, 1), (2, -1), (1, -2), (-1, -2), (-2, -1), (-2, 1), (-1, 2)];

    private static readonly (int File, int Rank)[] DiagonalDeltas = [(1, 1), (1, -1), (-1, -1), (-1, 1)];

    private static readonly (int File, int Rank)[] OrthogonalDeltas = [(0, 1), (1, 0), (0, -1), (-1, 0)];

    private static readonly (int File, int Rank)[] AllDeltas = [.. OrthogonalDeltas, .. DiagonalDeltas];

    private readonly BoardPiece?[] squares;

    private PieceBoard(BoardPiece?[] squares) => this.squares = squares;

    public Side SideToMove { get; private set; } = Side.White;

    public CastlingRights Castling { get; private set; } = CastlingRights.All;


    public Square? EnPassantTarget { get; private set; }

    public int HalfmoveClock { get; private set; }

    public int FullmoveNumber { get; private set; } = 1;

    /// <summary>When set, a capturing piece keeps its own powers and gains the captured piece's —
    /// the whole point of Absorption Chess, and off everywhere else.</summary>
    public bool AbsorbOnCapture { get; set; }

    public static PieceBoard StandardStart() => FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

    public static PieceBoard Empty() => new(new BoardPiece?[64]) { Castling = CastlingRights.None };

    public BoardPiece? At(Square square) => squares[Index(square)];

    public void Set(Square square, BoardPiece? piece) => squares[Index(square)] = piece;

    public void SetSideToMove(Side side) => SideToMove = side;

    public void ClearEnPassant() => EnPassantTarget = null;

    public void RevokeCastling(CastlingRights rights) => Castling &= ~rights;

    public PieceBoard Clone() => new((BoardPiece?[])squares.Clone())
    {
        SideToMove = SideToMove,
        Castling = Castling,
        EnPassantTarget = EnPassantTarget,
        HalfmoveClock = HalfmoveClock,
        FullmoveNumber = FullmoveNumber,
        AbsorbOnCapture = AbsorbOnCapture,
    };

    public Square? RoyalSquare(Side side)
    {
        for (var i = 0; i < squares.Length; i++)
        {
            if (squares[i] is { IsRoyal: true } piece && piece.Side == side)
                return SquareAt(i);
        }

        return null;
    }

    public bool IsRoyalAttacked(Side side) =>
        RoyalSquare(side) is { } royal && IsAttacked(royal, Opponent(side));

    public bool IsAttacked(Square square, Side by)
    {
        for (var i = 0; i < squares.Length; i++)
        {
            if (squares[i] is not { } piece || piece.Side != by)
                continue;

            if (Attacks(piece, SquareAt(i), square))
                return true;
        }

        return false;
    }

    /// <summary>Every move the side to move could make if nothing but the geometry of the pieces
    /// mattered: leaving your own royal piece attacked is allowed, and so is capturing the
    /// opponent's. That's exactly the rule set Bidding Chess plays by.</summary>
    public IReadOnlyList<ChessMove> PseudoLegalMoves() => PseudoLegalMoves(SideToMove);

    public IReadOnlyList<ChessMove> PseudoLegalMoves(Side side)
    {
        var moves = new List<ChessMove>();
        var seen = new HashSet<(Square From, Square To, PieceKind? Promote)>();

        for (var i = 0; i < squares.Length; i++)
        {
            if (squares[i] is not { } piece || piece.Side != side)
                continue;

            GenerateFor(piece, SquareAt(i), moves, seen);
        }

        AssignSan(moves);
        return moves;
    }

    /// <summary>Pseudo-legal moves minus the ones that would leave this side's own royal piece
    /// under attack — ordinary chess legality, decided by this board rather than a library.</summary>
    public IReadOnlyList<ChessMove> LegalMoves() => LegalMoves(SideToMove);

    public IReadOnlyList<ChessMove> LegalMoves(Side side)
    {
        var legal = new List<ChessMove>();

        foreach (var move in PseudoLegalMoves(side))
        {
            if (move.CapturedPiece == PieceKind.King)
                continue;

            var probe = Clone();
            probe.Apply(move, annotate: false);
            if (!probe.IsRoyalAttacked(side))
                legal.Add(move);
        }

        AssignSan(legal);
        return legal;
    }

    /// <summary>Plays <paramref name="move"/> and returns it with the notation filled in — the check
    /// and mate marks can only be known once the move has actually been made.</summary>
    public ChessMove Apply(ChessMove move, bool annotate = true)
    {
        var piece = At(move.From) ?? throw new InvalidOperationException($"No piece at {move.From}.");
        var captured = At(move.To);
        var isEnPassant = piece.Has(MovePower.Pawn) && captured is null &&
            move.From.File != move.To.File && EnPassantTarget == move.To;

        if (isEnPassant)
        {
            var capturedPawnSquare = new Square(move.To.File, move.From.Rank);
            captured = At(capturedPawnSquare);
            Set(capturedPawnSquare, null);
        }

        Set(move.From, null);

        var moved = piece;
        if (AbsorbOnCapture && captured is { } taken)
            moved = moved.With(taken.Powers);

        if (move.PromoteTo is { } promotion)
            moved = moved with { Powers = (moved.Powers & ~MovePower.Pawn) | BoardPiece.PowerOf(promotion) };

        Set(move.To, moved);

        if (piece.IsRoyal && Math.Abs(move.To.File - move.From.File) == 2)
            MoveCastlingRook(move.To);

        UpdateCastlingRights(move.From, move.To);

        EnPassantTarget = piece.Has(MovePower.Pawn) && Math.Abs(move.To.Rank - move.From.Rank) == 2
            ? new Square(move.From.File, (move.From.Rank + move.To.Rank) / 2)
            : null;

        HalfmoveClock = piece.Has(MovePower.Pawn) || captured is not null ? 0 : HalfmoveClock + 1;
        if (SideToMove == Side.Black)
            FullmoveNumber++;

        SideToMove = Opponent(piece.Side);

        var applied = move with { CapturedPiece = captured?.PrimaryKind };
        return annotate ? Annotate(applied, Opponent(piece.Side)) : applied;
    }

    private ChessMove Annotate(ChessMove move, Side opponent)
    {
        var isCheck = IsRoyalAttacked(opponent);
        var isMate = isCheck && LegalMoves(opponent).Count == 0;
        var suffix = isMate ? "#" : isCheck ? "+" : "";
        return move with { IsCheck = isCheck, IsCheckmate = isMate, San = move.San + suffix };
    }

    private void MoveCastlingRook(Square kingTo)
    {
        var rank = kingTo.Rank;
        var (rookFrom, rookTo) = kingTo.File == 6
            ? (new Square(7, rank), new Square(5, rank))
            : (new Square(0, rank), new Square(3, rank));

        var rook = At(rookFrom) ?? throw new InvalidOperationException($"No rook to castle with at {rookFrom}.");
        Set(rookFrom, null);
        Set(rookTo, rook);
    }

    private void UpdateCastlingRights(Square from, Square to)
    {
        foreach (var square in (Square[])[from, to])
        {
            Castling &= ~(square switch
            {
                { File: 4, Rank: 0 } => CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide,
                { File: 0, Rank: 0 } => CastlingRights.WhiteQueenSide,
                { File: 7, Rank: 0 } => CastlingRights.WhiteKingSide,
                { File: 4, Rank: 7 } => CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide,
                { File: 0, Rank: 7 } => CastlingRights.BlackQueenSide,
                { File: 7, Rank: 7 } => CastlingRights.BlackKingSide,
                _ => CastlingRights.None,
            });
        }
    }

    private void GenerateFor(BoardPiece piece, Square from, List<ChessMove> moves,
        HashSet<(Square, Square, PieceKind?)> seen)
    {
        if (piece.Has(MovePower.Pawn))
            GeneratePawn(piece, from, moves, seen);

        if (piece.Has(MovePower.Knight))
        {
            foreach (var delta in KnightDeltas)
                TryStep(piece, from, delta, moves, seen);
        }

        if (piece.Has(MovePower.King))
        {
            foreach (var delta in AllDeltas)
                TryStep(piece, from, delta, moves, seen);
        }

        if (piece.Has(MovePower.Bishop) || piece.Has(MovePower.Queen))
        {
            foreach (var delta in DiagonalDeltas)
                TrySlide(piece, from, delta, moves, seen);
        }

        if (piece.Has(MovePower.Rook) || piece.Has(MovePower.Queen))
        {
            foreach (var delta in OrthogonalDeltas)
                TrySlide(piece, from, delta, moves, seen);
        }

        if (piece.IsRoyal)
            GenerateCastling(piece, from, moves, seen);
    }

    private void GeneratePawn(BoardPiece piece, Square from, List<ChessMove> moves,
        HashSet<(Square, Square, PieceKind?)> seen)
    {
        var direction = piece.Side == Side.White ? 1 : -1;
        var homeRank = piece.Side == Side.White ? 1 : 6;

        var oneAhead = Offset(from, 0, direction);
        if (oneAhead is { } ahead && At(ahead) is null)
        {
            AddMove(piece, from, ahead, null, moves, seen);

            if (from.Rank == homeRank && Offset(from, 0, direction * 2) is { } twoAhead && At(twoAhead) is null)
                AddMove(piece, from, twoAhead, null, moves, seen);
        }

        foreach (var fileDelta in (int[])[-1, 1])
        {
            if (Offset(from, fileDelta, direction) is not { } target)
                continue;

            var occupant = At(target);
            if (occupant is { } victim && victim.Side != piece.Side)
                AddMove(piece, from, target, victim, moves, seen);
            else if (occupant is null && EnPassantTarget == target)
                AddMove(piece, from, target, At(new Square(target.File, from.Rank)), moves, seen);
        }
    }

    private void GenerateCastling(BoardPiece piece, Square from, List<ChessMove> moves,
        HashSet<(Square, Square, PieceKind?)> seen)
    {
        var rank = piece.Side == Side.White ? 0 : 7;
        if (from != new Square(4, rank))
            return;

        var kingSide = piece.Side == Side.White ? CastlingRights.WhiteKingSide : CastlingRights.BlackKingSide;
        var queenSide = piece.Side == Side.White ? CastlingRights.WhiteQueenSide : CastlingRights.BlackQueenSide;
        var opponent = Opponent(piece.Side);

        if (Castling.HasFlag(kingSide) &&
            At(new Square(5, rank)) is null && At(new Square(6, rank)) is null &&
            !IsAttacked(from, opponent) && !IsAttacked(new Square(5, rank), opponent) &&
            !IsAttacked(new Square(6, rank), opponent))
        {
            AddMove(piece, from, new Square(6, rank), null, moves, seen);
        }

        if (Castling.HasFlag(queenSide) &&
            At(new Square(3, rank)) is null && At(new Square(2, rank)) is null && At(new Square(1, rank)) is null &&
            !IsAttacked(from, opponent) && !IsAttacked(new Square(3, rank), opponent) &&
            !IsAttacked(new Square(2, rank), opponent))
        {
            AddMove(piece, from, new Square(2, rank), null, moves, seen);
        }
    }

    private void TryStep(BoardPiece piece, Square from, (int File, int Rank) delta, List<ChessMove> moves,
        HashSet<(Square, Square, PieceKind?)> seen)
    {
        if (Offset(from, delta.File, delta.Rank) is not { } target)
            return;

        var occupant = At(target);
        if (occupant is { } blocker && blocker.Side == piece.Side)
            return;

        AddMove(piece, from, target, occupant, moves, seen);
    }

    private void TrySlide(BoardPiece piece, Square from, (int File, int Rank) delta, List<ChessMove> moves,
        HashSet<(Square, Square, PieceKind?)> seen)
    {
        var current = from;
        while (Offset(current, delta.File, delta.Rank) is { } target)
        {
            current = target;
            var occupant = At(target);

            if (occupant is { } blocker)
            {
                if (blocker.Side != piece.Side)
                    AddMove(piece, from, target, occupant, moves, seen);

                return;
            }

            AddMove(piece, from, target, null, moves, seen);
        }
    }

    private static void AddMove(BoardPiece piece, Square from, Square to, BoardPiece? captured,
        List<ChessMove> moves, HashSet<(Square, Square, PieceKind?)> seen)
    {
        var lastRank = piece.Side == Side.White ? 7 : 0;
        if (piece.Has(MovePower.Pawn) && to.Rank == lastRank)
        {
            foreach (var promotion in (PieceKind[])[PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight])
            {
                if (seen.Add((from, to, promotion)))
                    moves.Add(NewMove(piece, from, to, captured, promotion));
            }

            return;
        }

        if (seen.Add((from, to, null)))
            moves.Add(NewMove(piece, from, to, captured, null));
    }

    private static ChessMove NewMove(BoardPiece piece, Square from, Square to, BoardPiece? captured,
        PieceKind? promoteTo) =>
        new(from, to, piece.PrimaryKind, captured?.PrimaryKind, promoteTo, false, false, string.Empty);

    private static void AssignSan(List<ChessMove> moves)
    {
        for (var i = 0; i < moves.Count; i++)
        {
            var move = moves[i];
            var ambiguous = moves.Where(other =>
                other.From != move.From && other.To == move.To && other.Piece == move.Piece).ToArray();

            moves[i] = move with { San = San(move, ambiguous) };
        }
    }

    private static string San(ChessMove move, IReadOnlyList<ChessMove> ambiguous)
    {
        if (move.Piece == PieceKind.King && Math.Abs(move.To.File - move.From.File) == 2)
            return move.To.File == 6 ? "O-O" : "O-O-O";

        var builder = new StringBuilder();

        if (move.Piece == PieceKind.Pawn)
        {
            if (move.CapturedPiece is not null)
                builder.Append((char)('a' + move.From.File)).Append('x');
        }
        else
        {
            builder.Append(Letter(move.Piece));

            if (ambiguous.Count > 0)
            {
                if (ambiguous.All(other => other.From.File != move.From.File))
                    builder.Append((char)('a' + move.From.File));
                else if (ambiguous.All(other => other.From.Rank != move.From.Rank))
                    builder.Append((char)('1' + move.From.Rank));
                else
                    builder.Append(move.From.ToString());
            }

            if (move.CapturedPiece is not null)
                builder.Append('x');
        }

        builder.Append(move.To);

        if (move.PromoteTo is { } promotion)
            builder.Append('=').Append(Letter(promotion));

        return builder.ToString();
    }

    private static char Letter(PieceKind kind) => kind switch
    {
        PieceKind.Knight => 'N',
        PieceKind.Bishop => 'B',
        PieceKind.Rook => 'R',
        PieceKind.Queen => 'Q',
        PieceKind.King => 'K',
        _ => 'P',
    };

    private bool Attacks(BoardPiece piece, Square from, Square target)
    {
        if (from == target)
            return false;

        var fileDelta = target.File - from.File;
        var rankDelta = target.Rank - from.Rank;

        if (piece.Has(MovePower.Pawn))
        {
            var direction = piece.Side == Side.White ? 1 : -1;
            if (rankDelta == direction && Math.Abs(fileDelta) == 1)
                return true;
        }

        if (piece.Has(MovePower.Knight) && KnightDeltas.Any(d => d.File == fileDelta && d.Rank == rankDelta))
            return true;

        if (piece.Has(MovePower.King) && Math.Abs(fileDelta) <= 1 && Math.Abs(rankDelta) <= 1)
            return true;

        var isDiagonal = Math.Abs(fileDelta) == Math.Abs(rankDelta);
        var isOrthogonal = fileDelta == 0 || rankDelta == 0;

        if (isDiagonal && (piece.Has(MovePower.Bishop) || piece.Has(MovePower.Queen)) && IsPathClear(from, target))
            return true;

        return isOrthogonal && (piece.Has(MovePower.Rook) || piece.Has(MovePower.Queen)) && IsPathClear(from, target);
    }

    private bool IsPathClear(Square from, Square to)
    {
        var fileStep = Math.Sign(to.File - from.File);
        var rankStep = Math.Sign(to.Rank - from.Rank);

        var current = from;
        while (Offset(current, fileStep, rankStep) is { } next && next != to)
        {
            current = next;
            if (At(current) is not null)
                return false;
        }

        return true;
    }

    public static PieceBoard FromFen(string fen)
    {
        var fields = fen.Split(' ');
        var board = Empty();

        var ranks = fields[0].Split('/');
        if (ranks.Length != 8)
            throw new FormatException($"Invalid FEN placement: '{fields[0]}'.");

        for (var rankFromTop = 0; rankFromTop < 8; rankFromTop++)
        {
            var rank = 7 - rankFromTop;
            var file = 0;
            foreach (var symbol in ranks[rankFromTop])
            {
                if (char.IsDigit(symbol))
                {
                    file += symbol - '0';
                    continue;
                }

                var side = char.IsUpper(symbol) ? Side.White : Side.Black;
                board.Set(new Square(file, rank), BoardPiece.Of(side, KindOf(char.ToLowerInvariant(symbol))));
                file++;
            }
        }

        board.SideToMove = fields.Length > 1 && fields[1] == "b" ? Side.Black : Side.White;
        board.Castling = fields.Length > 2 ? ParseCastling(fields[2]) : CastlingRights.None;
        board.EnPassantTarget = fields.Length > 3 && fields[3] != "-" ? Square.Parse(fields[3]) : null;
        board.HalfmoveClock = fields.Length > 4 ? int.Parse(fields[4]) : 0;
        board.FullmoveNumber = fields.Length > 5 ? int.Parse(fields[5]) : 1;
        return board;
    }

    /// <summary>A standard FEN of this position. Pieces that have absorbed extra powers are written
    /// as whatever they read as (see <see cref="BoardPiece.PrimaryKind"/>), so a board with stacked
    /// powers round-trips only through <see cref="PowersText"/> alongside it.</summary>
    public string ToFen()
    {
        var placement = new StringBuilder();

        for (var rank = 7; rank >= 0; rank--)
        {
            var empty = 0;
            for (var file = 0; file < 8; file++)
            {
                if (At(new Square(file, rank)) is not { } piece)
                {
                    empty++;
                    continue;
                }

                if (empty > 0)
                {
                    placement.Append(empty);
                    empty = 0;
                }

                var symbol = char.ToLowerInvariant(Letter(piece.PrimaryKind));
                placement.Append(piece.Side == Side.White ? char.ToUpperInvariant(symbol) : symbol);
            }

            if (empty > 0)
                placement.Append(empty);

            if (rank > 0)
                placement.Append('/');
        }

        var castling = CastlingText();
        var enPassant = EnPassantTarget?.ToString() ?? "-";
        var side = SideToMove == Side.White ? "w" : "b";
        return $"{placement} {side} {castling} {enPassant} {HalfmoveClock} {FullmoveNumber}";
    }

    /// <summary>The powers every piece that has more than its own carries, as "e4:RN" entries — the
    /// part of an Absorption Chess position a plain FEN can't hold.</summary>
    public string PowersText()
    {
        var entries = new List<string>();

        for (var i = 0; i < squares.Length; i++)
        {
            if (squares[i] is not { } piece)
                continue;

            var extra = piece.Powers & ~BoardPiece.PowerOf(piece.PrimaryKind);
            if (extra == MovePower.None)
                continue;

            var letters = new string(Enum.GetValues<MovePower>()
                .Where(power => power != MovePower.None && (extra & power) != 0)
                .Select(power => Letter(KindOfPower(power)))
                .ToArray());

            entries.Add($"{SquareAt(i)}:{letters}");
        }

        return string.Join(',', entries);
    }

    private string CastlingText()
    {
        var text = new StringBuilder();
        if (Castling.HasFlag(CastlingRights.WhiteKingSide)) text.Append('K');
        if (Castling.HasFlag(CastlingRights.WhiteQueenSide)) text.Append('Q');
        if (Castling.HasFlag(CastlingRights.BlackKingSide)) text.Append('k');
        if (Castling.HasFlag(CastlingRights.BlackQueenSide)) text.Append('q');
        return text.Length == 0 ? "-" : text.ToString();
    }

    private static CastlingRights ParseCastling(string text)
    {
        var rights = CastlingRights.None;
        if (text.Contains('K')) rights |= CastlingRights.WhiteKingSide;
        if (text.Contains('Q')) rights |= CastlingRights.WhiteQueenSide;
        if (text.Contains('k')) rights |= CastlingRights.BlackKingSide;
        if (text.Contains('q')) rights |= CastlingRights.BlackQueenSide;
        return rights;
    }

    private static PieceKind KindOf(char symbol) => symbol switch
    {
        'p' => PieceKind.Pawn,
        'n' => PieceKind.Knight,
        'b' => PieceKind.Bishop,
        'r' => PieceKind.Rook,
        'q' => PieceKind.Queen,
        'k' => PieceKind.King,
        _ => throw new FormatException($"Unknown piece symbol '{symbol}'."),
    };

    private static PieceKind KindOfPower(MovePower power) => power switch
    {
        MovePower.Pawn => PieceKind.Pawn,
        MovePower.Knight => PieceKind.Knight,
        MovePower.Bishop => PieceKind.Bishop,
        MovePower.Rook => PieceKind.Rook,
        MovePower.Queen => PieceKind.Queen,
        MovePower.King => PieceKind.King,
        _ => throw new ArgumentOutOfRangeException(nameof(power), power, "Unknown move power."),
    };

    private static Square? Offset(Square square, int fileDelta, int rankDelta)
    {
        var file = square.File + fileDelta;
        var rank = square.Rank + rankDelta;
        return file is >= 0 and <= 7 && rank is >= 0 and <= 7 ? new Square(file, rank) : null;
    }

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;

    private static int Index(Square square) => square.Rank * 8 + square.File;

    private static Square SquareAt(int index) => new(index % 8, index / 8);
}
