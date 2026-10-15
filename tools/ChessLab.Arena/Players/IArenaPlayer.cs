using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Players;

public interface IArenaPlayer : IAsyncDisposable
{
    Task<GameAction> ChooseAsync(BotActionRequest request, CancellationToken ct);
}

public interface IPlayerFactory
{
    string Name { get; }

    BotDifficulty Difficulty { get; }

    TimeSpan BudgetFor(GameKind kind);

    Task<IArenaPlayer> CreateAsync(GameKind kind, int seed, CancellationToken ct);
}
