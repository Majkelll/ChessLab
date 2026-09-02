using Chess;

namespace ChessLab.Core.Chess;

/// <summary>IChessRulesEngine backed by the Gera.Chess library.</summary>
public sealed class GeraChessRulesEngine : IChessRulesEngine
{
    private ChessBoard board;

    public GeraChessRulesEngine() => board = new ChessBoard { AutoEndgameRules = AutoEndgameRules.All };

    private GeraChessRulesEngine(ChessBoard board) => this.board = board;

    /// <summary>Creates an engine from a FEN position. Mainly useful for tests that need a specific position.</summary>
    public static GeraChessRulesEngine FromFen(string fen) =>
        new(ChessBoard.LoadFromFen(fen, AutoEndgameRules.All));

    public Side SideToMove => ToSide(board.Turn);

    public bool IsInCheck(Side side) => side == Side.White ? board.WhiteKingChecked : board.BlackKingChecked;

    public GameEndResult? EndResult
    {
        get
        {
            var end = board.EndGame;
            if (end is null)
                return null;

            return new GameEndResult(ToReason(end.EndgameType), end.WonSide is { } won ? ToSide(won) : null);
        }
    }

    public IReadOnlyList<ChessMove> LegalMoves() => board.Moves().Select(ToChessMove).ToArray();

    public IReadOnlyList<ChessMove> LegalMoves(PieceKind kind) =>
        LegalMoves().Where(m => m.Piece == kind).ToArray();

    /// <summary>For the side to move, this is just <see cref="LegalMoves(PieceKind)"/>. For the
    /// other side, there's no direct way to ask the underlying library "what if it were your turn" —
    /// so this loads a throwaway board from the current FEN with the active color swapped (and the
    /// en passant target cleared, since it wouldn't apply to a hypothetical turn) and asks that one
    /// instead. The library doesn't validate that the side not moving is out of check, so this is
    /// safe even when the real side to move is currently in check.</summary>
    public bool HasLegalMove(Side side, PieceKind kind)
    {
        if (side == SideToMove)
            return LegalMoves(kind).Count > 0;

        var probe = ChessBoard.LoadFromFen(WithSideToMove(board.ToFen(), side), AutoEndgameRules.All);
        return probe.Moves().Any(m => ToPieceKind(m.Piece.Type) == kind);
    }

    private static string WithSideToMove(string fen, Side side)
    {
        var fields = fen.Split(' ');
        fields[1] = side == Side.White ? "w" : "b";
        fields[3] = "-";
        return string.Join(' ', fields);
    }

    public void ApplyMove(ChessMove move)
    {
        var fromPosition = new Position((short)move.From.File, (short)move.From.Rank);
        var candidates = board.Moves(fromPosition);

        Move? match = null;
        foreach (var candidate in candidates)
        {
            if (ToSquare(candidate.NewPosition) != move.To)
                continue;

            var candidatePromotion = candidate.Promotion is { } promo ? ToPieceKind(promo.Type) : (PieceKind?)null;
            if (candidatePromotion != move.PromoteTo)
                continue;

            match = candidate;
            break;
        }

        if (match is null || !board.Move(match))
            throw new InvalidOperationException($"Move {move} is not legal in the current position.");
    }

    public void Resign(Side side) => board.Resign(ToPieceColor(side));

    public void DeclareTimeout(Side side) => board.EndByTimeout(ToPieceColor(side));

    public string ToFen() => board.ToFen();

    public void LoadPosition(string fen) => board = ChessBoard.LoadFromFen(fen, AutoEndgameRules.All);

    private static ChessMove ToChessMove(Move m) => new(
        From: ToSquare(m.OriginalPosition),
        To: ToSquare(m.NewPosition),
        Piece: ToPieceKind(m.Piece.Type),
        CapturedPiece: m.CapturedPiece is { } captured ? ToPieceKind(captured.Type) : null,
        PromoteTo: m.Promotion is { } promotion ? ToPieceKind(promotion.Type) : null,
        IsCheck: m.IsCheck,
        IsCheckmate: m.IsMate,
        San: m.San ?? string.Empty);

    private static Square ToSquare(Position position) => new(position.X, position.Y);

    private static PieceKind ToPieceKind(PieceType type) => type.AsChar switch
    {
        'p' => PieceKind.Pawn,
        'r' => PieceKind.Rook,
        'n' => PieceKind.Knight,
        'b' => PieceKind.Bishop,
        'q' => PieceKind.Queen,
        'k' => PieceKind.King,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown piece type."),
    };

    private static Side ToSide(PieceColor color) => color.AsChar switch
    {
        'w' => Side.White,
        'b' => Side.Black,
        _ => throw new ArgumentOutOfRangeException(nameof(color), color, "Unknown piece color."),
    };

    private static PieceColor ToPieceColor(Side side) => side == Side.White ? PieceColor.White : PieceColor.Black;

    private static GameEndReason ToReason(EndgameType type) => type switch
    {
        EndgameType.Checkmate => GameEndReason.Checkmate,
        EndgameType.Stalemate => GameEndReason.Stalemate,
        EndgameType.InsufficientMaterial => GameEndReason.InsufficientMaterial,
        EndgameType.FiftyMoveRule => GameEndReason.FiftyMoveRule,
        EndgameType.Repetition => GameEndReason.Repetition,
        EndgameType.Resigned => GameEndReason.Resignation,
        EndgameType.Timeout => GameEndReason.Timeout,
        EndgameType.DrawDeclared => GameEndReason.DrawAgreed,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown endgame type."),
    };
}
