using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public interface IGameBot
{
    Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default);
}

public static class GameBots
{
    public static bool NeedsChessEngine(GameKind kind) =>
        kind is GameKind.HandAndBrain or GameKind.CardChess or GameKind.ArcaneChess or GameKind.ProgressiveChess;

    public static IGameBot For(GameKind kind, IChessEngine? engine, int? seed = null) => kind switch
    {
        GameKind.HandAndBrain => new HandBrainBot(Required(engine)),
        GameKind.CardChess => new CardChessBot(Required(engine)),
        GameKind.ArcaneChess => new ArcaneChessBot(Required(engine), seed),
        GameKind.ProgressiveChess => new ProgressiveChessBot(Required(engine)),
        GameKind.BiddingChess => new BiddingChessBot(seed),
        GameKind.AliceChess => new AliceChessBot(seed),
        GameKind.AbsorptionChess => new AbsorptionChessBot(seed),
        GameKind.MartianChess => new MartianChessBot(seed),
        GameKind.DraftChess => new DraftChessBot(seed),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No bot for this game kind."),
    };

    internal static GameAction MoveAction(ChessMove move) => GameAction.MovePiece(move.From, move.To, move.PromoteTo);

    private static IChessEngine Required(IChessEngine? engine) =>
        engine ?? throw new ArgumentNullException(nameof(engine), "This game kind's bot needs a chess engine.");
}
