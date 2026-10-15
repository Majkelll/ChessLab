using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Engines;

[Flags]
public enum MartianBug
{
    None = 0,
    NoLongDiagonals = 1,
    UndoKeepsScore = 2,
    HashIgnoresScore = 4,
    EvaluationFavoursWhite = 8,
    AllocatesPerNode = 16,
    EvaluationIgnoresMaterial = 32,
}

public sealed class SampleMartianEngine : IEngineUnderTest
{
    private const int Files = 4;
    private const int Squares = 32;
    private const int MoveLimit = 300;
    private const int MaxPly = 512;
    private const int MaxMoves = 64;
    private const int NoSquare = -1;

    private static readonly (int File, int Rank)[] Diagonal = [(1, 1), (1, -1), (-1, -1), (-1, 1)];
    private static readonly (int File, int Rank)[] Orthogonal = [(0, 1), (1, 0), (0, -1), (-1, 0)];
    private static readonly (int File, int Rank)[] AllSteps = [.. Orthogonal, .. Diagonal];

    private static readonly ulong[,] PieceKeys = new ulong[Squares, 4];
    private static readonly ulong[,] ScoreKeys = new ulong[2, 64];
    private static readonly ulong[] LastMoveKeys = new ulong[(Squares * Squares) + 1];
    private static readonly ulong BlackToMoveKey;

    private readonly MartianBug bugs;
    private readonly int[] board = new int[Squares];
    private readonly int[] scores = new int[2];
    private readonly UndoEntry[] undo = new UndoEntry[MaxPly];
    private readonly int[][] moveBuffers = new int[MaxPly][];
    private int side;
    private int lastFrom = NoSquare;
    private int lastTo = NoSquare;
    private int plies;
    private int undoCount;
    private ulong hash;
    private long nodes;

    static SampleMartianEngine()
    {
        var random = new Random(20260101);
        for (var square = 0; square < Squares; square++)
            for (var value = 1; value < 4; value++)
                PieceKeys[square, value] = NextKey(random);

        for (var player = 0; player < 2; player++)
            for (var score = 0; score < 64; score++)
                ScoreKeys[player, score] = NextKey(random);

        for (var i = 0; i < LastMoveKeys.Length; i++)
            LastMoveKeys[i] = NextKey(random);

        BlackToMoveKey = NextKey(random);
    }

    public SampleMartianEngine(MartianBug bugs = MartianBug.None)
    {
        this.bugs = bugs;
        for (var ply = 0; ply < MaxPly; ply++)
            moveBuffers[ply] = new int[MaxMoves];
    }

    public GameKind Kind => GameKind.MartianChess;

    public string Name => "sample-martian";

    public bool AllocationFreeSearch => true;

    public ulong Hash => hash;

    public string PositionText
    {
        get
        {
            var rows = new List<string>();
            for (var rank = 7; rank >= 0; rank--)
            {
                var row = new char[Files];
                for (var file = 0; file < Files; file++)
                    row[file] = board[(rank * Files) + file] is var value and > 0 ? (char)('0' + value) : '.';
                rows.Add(new string(row));
            }

            return $"{string.Join('/', rows)} {(side == 0 ? 'W' : 'B')} {scores[0]}-{scores[1]} last {lastFrom}-{lastTo}";
        }
    }

    public void Load(PositionLine position)
    {
        Array.Clear(board);
        Array.Clear(scores);
        (int File, int Rank, int Value)[] corner =
        [
            (0, 0, 3), (1, 0, 3), (2, 0, 2),
            (0, 1, 3), (1, 1, 2), (2, 1, 1),
            (0, 2, 2), (1, 2, 1), (2, 2, 1),
        ];
        foreach (var (file, rank, value) in corner)
        {
            board[(rank * Files) + file] = value;
            board[((7 - rank) * Files) + (Files - 1 - file)] = value;
        }

        side = 0;
        lastFrom = lastTo = NoSquare;
        plies = 0;
        undoCount = 0;
        hash = ComputeHashFromScratch();

        foreach (var action in position.Actions)
            Apply(action);
    }

    public IReadOnlyList<string> LegalActions()
    {
        var buffer = new int[MaxMoves];
        var count = Generate(buffer);
        var actions = new List<string>(count);
        for (var i = 0; i < count; i++)
            actions.Add($"{(side == 0 ? 'w' : 'b')}:{SquareName(From(buffer[i]))}{SquareName(To(buffer[i]))}");

        return actions;
    }

    public void Apply(string action)
    {
        var expected = side == 0 ? "w:" : "b:";
        if (!action.StartsWith(expected, StringComparison.Ordinal) || action.Length != 6)
            throw new InvalidOperationException($"{action} is not a move for the side to move.");

        var move = Encode(ParseSquare(action.AsSpan(2, 2)), ParseSquare(action.AsSpan(4, 2)));
        var buffer = new int[MaxMoves];
        var count = Generate(buffer);
        if (Array.IndexOf(buffer, move, 0, count) < 0)
            throw new InvalidOperationException($"{action} is not legal.");

        Make(move);
    }

    public void Undo()
    {
        if (undoCount == 0)
            throw new InvalidOperationException("There is nothing to undo.");

        Unmake();
    }

    public ulong ComputeHashFromScratch()
    {
        var key = side == 1 ? BlackToMoveKey : 0UL;
        for (var square = 0; square < Squares; square++)
            if (board[square] > 0)
                key ^= PieceKeys[square, board[square]];

        key ^= ScoreKey(0, scores[0]) ^ ScoreKey(1, scores[1]);
        return key ^ LastMoveKeys[LastMoveIndex(lastFrom, lastTo)];
    }

    public int Evaluate()
    {
        var material = 0;
        for (var square = 0; square < Squares; square++)
            material += HalfOf(square) == 0 ? board[square] : -board[square];

        var bias = Has(MartianBug.EvaluationFavoursWhite) ? 7 : 0;
        var materialWeight = Has(MartianBug.EvaluationIgnoresMaterial) ? 0 : 10;
        return (100 * (scores[0] - scores[1])) + (materialWeight * material) + bias;
    }

    public bool TryMirror()
    {
        Array.Reverse(board);
        (scores[0], scores[1]) = (scores[1], scores[0]);
        side = 1 - side;
        if (lastFrom != NoSquare)
            (lastFrom, lastTo) = (Squares - 1 - lastFrom, Squares - 1 - lastTo);

        hash = ComputeHashFromScratch();
        return true;
    }

    public SearchResult Search(int depth)
    {
        nodes = 0;
        var bestMove = 0;
        var score = Negamax(depth, 0, -1_000_000, 1_000_000, ref bestMove);
        var best = bestMove == 0 ? null : $"{(side == 0 ? 'w' : 'b')}:{SquareName(From(bestMove))}{SquareName(To(bestMove))}";
        return new SearchResult(nodes, best, side == 0 ? score : -score);
    }

    private int Negamax(int depth, int ply, int alpha, int beta, ref int bestMove)
    {
        nodes++;
        if (Has(MartianBug.AllocatesPerNode))
            GC.KeepAlive(new int[8]);

        var moves = moveBuffers[ply];
        var count = Generate(moves);
        if (depth == 0 || count == 0)
            return side == 0 ? Evaluate() : -Evaluate();

        var best = int.MinValue;
        for (var i = 0; i < count; i++)
        {
            Make(moves[i]);
            var ignored = 0;
            var score = -Negamax(depth - 1, ply + 1, -beta, -alpha, ref ignored);
            Unmake();

            if (score > best)
            {
                best = score;
                if (ply == 0)
                    bestMove = moves[i];
            }

            alpha = Math.Max(alpha, score);
            if (alpha >= beta)
                break;
        }

        return best;
    }

    private int Generate(int[] moves)
    {
        if (IsHalfEmpty(0) || IsHalfEmpty(1) || plies >= MoveLimit)
            return 0;

        var count = 0;
        for (var from = 0; from < Squares; from++)
        {
            var value = board[from];
            if (value == 0 || HalfOf(from) != side)
                continue;

            if (value == 1)
            {
                foreach (var step in Diagonal)
                    count = TryAdd(moves, count, from, Offset(from, step));
                continue;
            }

            var steps = value == 2 ? Orthogonal : AllSteps;
            var range = value == 2 ? 2 : 8;
            foreach (var step in steps)
            {
                if (value == 3 && Has(MartianBug.NoLongDiagonals) && step.File != 0 && step.Rank != 0)
                    range = 1;

                var current = from;
                for (var distance = 0; distance < range; distance++)
                {
                    current = Offset(current, step);
                    if (current == NoSquare)
                        break;

                    count = TryAdd(moves, count, from, current);
                    if (board[current] != 0)
                        break;
                }

                range = value == 2 ? 2 : 8;
            }
        }

        return count;
    }

    private int TryAdd(int[] moves, int count, int from, int to)
    {
        if (to == NoSquare || (from == lastTo && to == lastFrom))
            return count;

        if (board[to] != 0 && HalfOf(to) == side)
            return count;

        moves[count] = Encode(from, to);
        return count + 1;
    }

    private void Make(int move)
    {
        var (from, to) = (From(move), To(move));
        undo[undoCount++] = new UndoEntry(move, board[to], lastFrom, lastTo, hash);

        var captured = board[to];
        var moving = board[from];
        hash ^= PieceKeys[from, moving];
        if (captured > 0)
            hash ^= PieceKeys[to, captured];
        hash ^= PieceKeys[to, moving];

        if (!Has(MartianBug.HashIgnoresScore))
            hash ^= ScoreKey(side, scores[side]) ^ ScoreKey(side, scores[side] + captured);

        hash ^= LastMoveKeys[LastMoveIndex(lastFrom, lastTo)] ^ LastMoveKeys[LastMoveIndex(from, to)];
        hash ^= BlackToMoveKey;

        board[to] = moving;
        board[from] = 0;
        scores[side] += captured;
        lastFrom = from;
        lastTo = to;
        side = 1 - side;
        plies++;
    }

    private void Unmake()
    {
        var entry = undo[--undoCount];
        var (from, to) = (From(entry.Move), To(entry.Move));
        side = 1 - side;
        plies--;
        board[from] = board[to];
        board[to] = entry.Captured;
        if (!Has(MartianBug.UndoKeepsScore))
            scores[side] -= entry.Captured;
        lastFrom = entry.LastFrom;
        lastTo = entry.LastTo;
        hash = entry.Hash;
    }

    private bool Has(MartianBug bug) => (bugs & bug) != 0;

    private static ulong ScoreKey(int player, int score) => ScoreKeys[player, score & 63];

    private bool IsHalfEmpty(int half)
    {
        for (var square = half * 16; square < (half + 1) * 16; square++)
            if (board[square] != 0)
                return false;

        return true;
    }

    private static int HalfOf(int square) => square < 16 ? 0 : 1;

    private static int Offset(int square, (int File, int Rank) step)
    {
        var file = (square % Files) + step.File;
        var rank = (square / Files) + step.Rank;
        return file is >= 0 and < Files && rank is >= 0 and < 8 ? (rank * Files) + file : NoSquare;
    }

    private static int Encode(int from, int to) => ((from + 1) << 8) | (to + 1);

    private static int From(int move) => (move >> 8) - 1;

    private static int To(int move) => (move & 0xFF) - 1;

    private static int LastMoveIndex(int from, int to) => from == NoSquare ? Squares * Squares : (from * Squares) + to;

    private static string SquareName(int square) => $"{(char)('a' + (square % Files))}{(square / Files) + 1}";

    private static int ParseSquare(ReadOnlySpan<char> text) => ((text[1] - '1') * Files) + (text[0] - 'a');

    private static ulong NextKey(Random random) => unchecked((ulong)random.NextInt64());

    private readonly record struct UndoEntry(int Move, int Captured, int LastFrom, int LastTo, ulong Hash);
}
