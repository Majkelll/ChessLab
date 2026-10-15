using ChessLab.Bots;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Players;

public sealed class BotPlayerFactory(BotDifficulty difficulty, ArenaSettings settings) : IPlayerFactory
{
    public string Name => $"bot:{Difficulty}";

    public BotDifficulty Difficulty => difficulty;

    public TimeSpan BudgetFor(GameKind kind) =>
        settings.BudgetOverride ?? (GameBots.NeedsChessEngine(kind)
            ? TimeSpan.FromMilliseconds(2 * DifficultyPresets.For(difficulty).MovetimeMs)
            : ArenaSettings.DefaultBudget);

    public async Task<IArenaPlayer> CreateAsync(GameKind kind, int seed, CancellationToken ct)
    {
        var engine = GameBots.NeedsChessEngine(kind)
            ? await StockfishEngine.StartAsync(settings.StockfishPath, ct)
            : null;

        await BotWarmUp.OnceAsync(kind, difficulty, engine, ct);
        return new BotPlayer(GameBots.For(kind, engine, seed), engine);
    }

    private sealed class BotPlayer(IGameBot bot, IChessEngineProcess? engine) : IArenaPlayer
    {
        public Task<GameAction> ChooseAsync(BotActionRequest request, CancellationToken ct) =>
            bot.ChooseActionAsync(request.State, request.Seat, request.Difficulty, ct);

        public async ValueTask DisposeAsync()
        {
            if (engine is not null)
                await engine.DisposeAsync();
        }
    }
}
