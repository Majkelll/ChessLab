using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Martian;

/// <summary>
/// Martian Chess (Looney Labs): not chess at all, and the one mode here with no king, no check and
/// no colours. The board is four files by eight ranks, split into two halves, and you own whatever
/// stands in your half — so a piece you push across the middle becomes your opponent's. Pyramids
/// come in three sizes: a 1 steps one square diagonally, a 2 moves one or two squares straight, and
/// a 3 moves any distance in any direction; none of them jump. Landing on a piece in the opponent's
/// half takes it for its face value in points, landing on one in your own half is simply not a move,
/// and you may not undo your opponent's last move by pushing the same piece straight back. When
/// either half falls empty the game stops and the higher score wins.
/// </summary>
public sealed class GameState : IGameEngineState
{
    public const int Files = 4;
    public const int Ranks = 8;
    public const int MoveLimit = 300;

    private static readonly (int File, int Rank)[] DiagonalSteps = [(1, 1), (1, -1), (-1, -1), (-1, 1)];
    private static readonly (int File, int Rank)[] OrthogonalSteps = [(0, 1), (1, 0), (0, -1), (-1, 0)];
    private static readonly (int File, int Rank)[] AllSteps = [.. OrthogonalSteps, .. DiagonalSteps];

    private readonly int[,] pyramids = new int[Files, Ranks];
    private readonly Dictionary<Side, int> scores;
    private readonly List<ChessMove> moveHistory = [];
    private readonly List<string> notations = [];
    private IReadOnlyList<ChessMove>? availableMoves;
    private (Square From, Square To)? lastMove;
    private GameEndResult? result;

    public GameState(Clock clock)
    {
        Clock = clock;
        scores = new Dictionary<Side, int> { [Side.White] = 0, [Side.Black] = 0 };
        Deal();
    }

    private void Deal()
    {
        (int File, int Rank, int Value)[] whiteCorner =
        [
            (0, 0, 3), (1, 0, 3), (2, 0, 2),
            (0, 1, 3), (1, 1, 2), (2, 1, 1),
            (0, 2, 2), (1, 2, 1), (2, 2, 1),
        ];

        foreach (var (file, rank, value) in whiteCorner)
        {
            pyramids[file, rank] = value;
            pyramids[Files - 1 - file, Ranks - 1 - rank] = value;
        }
    }

    public Clock Clock { get; }

    public Side SideToMove { get; private set; } = Side.White;

    public GameEndResult? EndResult => result;

    public bool IsGameOver => result is not null;

    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public IReadOnlyList<string> MoveNotations => notations;

    public IReadOnlyList<Side> SidesOnTheClock => [SideToMove];

    public int ScoreOf(Side side) => scores[side];

    /// <summary>The pyramid standing on <paramref name="square"/> as its point value, or 0 for an
    /// empty square.</summary>
    public int PyramidAt(Square square) => pyramids[square.File, square.Rank];

    /// <summary>Whose half <paramref name="square"/> belongs to — which is the only sense in which a
    /// piece belongs to anyone.</summary>
    public static Side HalfOf(Square square) => square.Rank < Ranks / 2 ? Side.White : Side.Black;

    /// <summary>A plain-text board with a digit per pyramid, ranks from the top down, plus both
    /// scores — there's no FEN for a game with no colours and no kings.</summary>
    public string PositionText
    {
        get
        {
            var rows = new List<string>();
            for (var rank = Ranks - 1; rank >= 0; rank--)
            {
                var row = new char[Files];
                for (var file = 0; file < Files; file++)
                {
                    var value = pyramids[file, rank];
                    row[file] = value == 0 ? '.' : (char)('0' + value);
                }

                rows.Add(new string(row));
            }

            return $"{string.Join('/', rows)} {SideToMove.ToString()[0]} {scores[Side.White]}-{scores[Side.Black]}";
        }
    }

    public IReadOnlyList<ChessMove> AvailableMoves => availableMoves ??= IsGameOver ? [] : LegalMoves(SideToMove);

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, TimeSpan elapsed)
    {
        EnsureNotOver();

        if (promoteTo is not null)
            throw new InvalidOperationException("There is no promotion in Martian Chess.");

        var mover = SideToMove;
        ChessMove? match = null;
        foreach (var candidate in AvailableMoves)
        {
            if (candidate.From == from && candidate.To == to)
            {
                match = candidate;
                break;
            }
        }

        if (match is null)
            throw new InvalidOperationException($"{from}-{to} is not a legal move right now.");

        var applied = match.Value;
        var captured = pyramids[to.File, to.Rank];
        pyramids[to.File, to.Rank] = pyramids[from.File, from.Rank];
        pyramids[from.File, from.Rank] = 0;
        scores[mover] += captured;

        moveHistory.Add(applied);
        notations.Add(applied.San);
        lastMove = (from, to);
        availableMoves = null;

        Clock.Deduct(mover, elapsed);
        Clock.ApplyIncrement(mover);

        SideToMove = Opponent(mover);
        DecideOutcome();
        return applied;
    }

    private void DecideOutcome()
    {
        if (IsHalfEmpty(Side.White) || IsHalfEmpty(Side.Black))
        {
            result = new GameEndResult(GameEndReason.BoardHalfEmptied, LeadingSide());
            return;
        }

        if (AvailableMoves.Count == 0)
        {
            result = new GameEndResult(GameEndReason.Stalemate, LeadingSide());
            return;
        }

        if (moveHistory.Count >= MoveLimit)
            result = new GameEndResult(GameEndReason.MoveLimit, LeadingSide());
    }

    private bool IsHalfEmpty(Side side)
    {
        for (var file = 0; file < Files; file++)
        {
            for (var rank = 0; rank < Ranks; rank++)
            {
                if (pyramids[file, rank] != 0 && HalfOf(new Square(file, rank)) == side)
                    return false;
            }
        }

        return true;
    }

    private Side? LeadingSide()
    {
        if (scores[Side.White] == scores[Side.Black])
            return null;

        return scores[Side.White] > scores[Side.Black] ? Side.White : Side.Black;
    }

    private IReadOnlyList<ChessMove> LegalMoves(Side side)
    {
        var moves = new List<ChessMove>();

        for (var file = 0; file < Files; file++)
        {
            for (var rank = 0; rank < Ranks; rank++)
            {
                var from = new Square(file, rank);
                var value = pyramids[file, rank];
                if (value == 0 || HalfOf(from) != side)
                    continue;

                foreach (var to in Destinations(from, value))
                {
                    if (lastMove is { } previous && previous.From == to && previous.To == from)
                        continue;

                    var target = pyramids[to.File, to.Rank];
                    if (target != 0 && HalfOf(to) == side)
                        continue;

                    moves.Add(NewMove(from, to, value, target));
                }
            }
        }

        return moves;
    }

    private IEnumerable<Square> Destinations(Square from, int value)
    {
        if (value == 1)
        {
            foreach (var step in DiagonalSteps)
            {
                if (Offset(from, step) is { } target)
                    yield return target;
            }

            yield break;
        }

        var steps = value == 2 ? OrthogonalSteps : AllSteps;
        var range = value == 2 ? 2 : Math.Max(Files, Ranks);

        foreach (var step in steps)
        {
            var current = from;
            for (var distance = 0; distance < range; distance++)
            {
                if (Offset(current, step) is not { } target)
                    break;

                current = target;
                yield return current;

                if (pyramids[current.File, current.Rank] != 0)
                    break;
            }
        }
    }

    private static ChessMove NewMove(Square from, Square to, int value, int captured)
    {
        var symbol = value == 1 ? "P" : value == 2 ? "D" : "Q";
        var san = $"{symbol}{from}{(captured == 0 ? "-" : "x")}{to}";
        return new ChessMove(from, to, KindOf(value), captured == 0 ? null : KindOf(captured), null, false, false, san);
    }

    /// <summary>Pyramids aren't chess pieces, but every mode speaks <see cref="ChessMove"/>, so each
    /// size travels as the chess piece the UI draws for it.</summary>
    public static PieceKind KindOf(int value) => value switch
    {
        1 => PieceKind.Pawn,
        2 => PieceKind.Knight,
        3 => PieceKind.Queen,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "A pyramid is worth 1, 2 or 3."),
    };

    private static Square? Offset(Square square, (int File, int Rank) step)
    {
        var file = square.File + step.File;
        var rank = square.Rank + step.Rank;
        return file >= 0 && file < Files && rank >= 0 && rank < Ranks ? new Square(file, rank) : null;
    }

    public void Resign(Side side)
    {
        EnsureNotOver();
        result = new GameEndResult(GameEndReason.Resignation, Opponent(side));
    }

    public void DeclareTimeoutIfFlagged()
    {
        if (IsGameOver)
            return;

        if (Clock.IsFlagged(SideToMove))
            result = new GameEndResult(GameEndReason.Timeout, Opponent(SideToMove));
    }

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }
}
