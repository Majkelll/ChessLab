using ChessLab.Bots;

namespace ChessLab.Core.Tests.Bots;

internal sealed class FakeChessEngine(Func<IReadOnlyList<string>, int>? score = null) : IChessEngine
{
    public List<(string Fen, IReadOnlyList<string> Candidates, int Elo, int MovetimeMs)> Searches { get; } = [];

    public Task<(string BestMoveUci, int? ScoreCentipawns)> GoAsync(string fen, IReadOnlyList<string> searchMovesUci,
        int elo, int movetimeMs, CancellationToken ct = default)
    {
        Searches.Add((fen, searchMovesUci, elo, movetimeMs));
        return Task.FromResult((searchMovesUci[^1], (int?)(score?.Invoke(searchMovesUci) ?? 0)));
    }
}
