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
    public string Fen { get; set; } = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
    public bool InCheck { get; set; } = true;

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
