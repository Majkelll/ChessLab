using ChessLab.Bots;

namespace ChessLab.BotService;

public interface IChessEngineFactory
{
    Task<IChessEngineProcess> StartAsync(CancellationToken ct);
}

public sealed class StockfishEngineFactory(string executablePath) : IChessEngineFactory
{
    public async Task<IChessEngineProcess> StartAsync(CancellationToken ct) =>
        await StockfishEngine.StartAsync(executablePath, ct);
}
