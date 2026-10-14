using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using GameState = ChessLab.Core.Draft.GameState;

namespace ChessLab.Bots;

public sealed class DraftChessBot : IGameBot
{
    private static readonly PieceKind[] ByWeight =
        [PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.Pawn];

    private static readonly int[] BackRankOrder = [4, 3, 5, 2, 6, 1, 7, 0];

    private readonly Random random = new();

    public Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var section = state.Draft ?? throw new InvalidOperationException("The state has no Draft Chess section.");

        return Task.FromResult(section.Phase switch
        {
            DraftPhase.Drafting => ChoosePick(section, seat.Side, difficulty),
            DraftPhase.Placing => ChoosePlacement(section, seat.Side),
            _ => GameBots.MoveAction(GreedyPlay.Choose(state.AvailableMoves, difficulty, random)),
        });
    }

    private GameAction ChoosePick(DraftSectionDto section, Side side, BotDifficulty difficulty)
    {
        var affordable = ByWeight.Where(kind => CanPick(section, side, kind)).ToArray();
        if (affordable.Length == 0)
            return GameAction.DraftPass();

        if (difficulty == BotDifficulty.Easy)
            return GameAction.DraftPick(affordable[random.Next(affordable.Length)]);

        return GameAction.DraftPick(affordable[0]);
    }

    private static bool CanPick(DraftSectionDto section, Side side, PieceKind kind)
    {
        if (section.Pool.FirstOrDefault(entry => entry.Kind == kind) is not { Remaining: > 0 } entry)
            return false;

        var budgetLeft = side == Side.White ? section.WhiteBudgetLeft : section.BlackBudgetLeft;
        if (entry.Cost > budgetLeft)
            return false;

        var picks = side == Side.White ? section.WhitePicks : section.BlackPicks;
        return kind == PieceKind.Pawn
            ? picks.Count(pick => pick == PieceKind.Pawn) < GameState.MaxPawns
            : picks.Count(pick => pick != PieceKind.Pawn) < GameState.MaxOfficers;
    }

    private static GameAction ChoosePlacement(DraftSectionDto section, Side side)
    {
        var backRank = side == Side.White ? 0 : 7;
        var pawnRank = side == Side.White ? 1 : 6;
        var placeable = (side == Side.White ? section.WhitePlaceable : section.BlackPlaceable)
            .ToDictionary(entry => entry.Kind, entry => entry.Squares);

        if (placeable.TryGetValue(PieceKind.King, out var kingSquares))
            return GameAction.PlacePiece(PieceKind.King, FirstFree(kingSquares, backRank));

        foreach (var kind in ByWeight)
        {
            if (!placeable.TryGetValue(kind, out var squares))
                continue;

            var rank = kind == PieceKind.Pawn ? pawnRank : backRank;
            return GameAction.PlacePiece(kind, FirstFree(squares, rank));
        }

        throw new InvalidOperationException("Everything is already laid out.");
    }

    private static Square FirstFree(IReadOnlyList<Square> placeable, int rank)
    {
        foreach (var file in BackRankOrder)
        {
            var square = new Square(file, rank);
            if (placeable.Contains(square))
                return square;
        }

        throw new InvalidOperationException($"Rank {rank + 1} is full.");
    }
}
