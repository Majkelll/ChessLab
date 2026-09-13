using ChessLab.Core.Chess;

namespace ChessLab.Core.Tests.CardChess;

internal sealed class FakeChessRulesEngine : IChessRulesEngine
{
    public Side SideToMove { get; set; } = Side.White;
    public GameEndResult? EndResult { get; set; }
    public List<ChessMove> AllMoves { get; set; } = [];
    public Dictionary<PieceKind, List<ChessMove>> MovesByKind { get; } = new();
    public List<ChessMove> AppliedMoves { get; } = [];
    public Side? Resigned { get; private set; }
    public Side? TimedOut { get; private set; }
    public string Fen { get; set; } = "8/8/8/8/8/8/8/8 w - - 0 1";
    public bool InCheck { get; set; } = true;

    /// <summary>When true, every card is immediately dealable/keepable (see
    /// <see cref="HasLegalMove"/>) regardless of <see cref="MovesByKind"/> — lets a test deal a
    /// hand deterministically (no burn-through-the-deck reshuffling) without also having to make
    /// every rank's <see cref="LegalMoves(PieceKind)"/> non-empty, which would defeat tests relying
    /// on "no hand card has a legal move" fallback behavior. Defaults to false to keep existing
    /// tests' behavior unchanged.</summary>
    public bool AlwaysHasLegalMove { get; set; }

    public bool IsInCheck(Side side) => InCheck;

    public IReadOnlyList<ChessMove> LegalMoves() => AllMoves;

    public IReadOnlyList<ChessMove> LegalMoves(PieceKind kind) =>
        MovesByKind.TryGetValue(kind, out var moves) ? moves : [];

    public bool HasLegalMove(Side side, PieceKind kind) => AlwaysHasLegalMove || LegalMoves(kind).Count > 0;

    public void ApplyMove(ChessMove move)
    {
        AppliedMoves.Add(move);
        SideToMove = SideToMove == Side.White ? Side.Black : Side.White;
    }

    public void Resign(Side side) => Resigned = side;

    public void DeclareTimeout(Side side) => TimedOut = side;

    public string ToFen() => Fen;

    public void LoadPosition(string fen)
    {
        Fen = fen;
        SideToMove = fen.Split(' ')[1] == "w" ? Side.White : Side.Black;
    }
}
