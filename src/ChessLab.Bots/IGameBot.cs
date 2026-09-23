using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

/// <summary>One bot-occupied turn's worth of decision, whatever the game kind is. The runner calls
/// this again as long as the active seat stays the bot's, so a mode where a turn takes more than
/// one action (Arcane Chess casting before moving) returns them one at a time.</summary>
public interface IGameBot
{
    Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default);
}

public static class GameBots
{
    public static IGameBot For(GameKind kind, StockfishEngine engine) => kind switch
    {
        GameKind.HandAndBrain => new HandBrainBot(engine),
        GameKind.CardChess => new CardChessBot(engine),
        GameKind.ArcaneChess => new ArcaneChessBot(engine),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No bot for this game kind."),
    };
}
