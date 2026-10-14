namespace ChessLab.Bots;

public interface IChessEngine
{
    Task<(string BestMoveUci, int? ScoreCentipawns)> GoAsync(
        string fen, IReadOnlyList<string> searchMovesUci, int elo, int movetimeMs, CancellationToken ct = default);
}

public interface IChessEngineProcess : IChessEngine, IAsyncDisposable
{
    bool HasExited { get; }
}
