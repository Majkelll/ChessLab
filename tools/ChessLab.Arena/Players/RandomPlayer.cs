using ChessLab.Arena.Refereeing;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Players;

public sealed class RandomPlayerFactory(ArenaSettings settings) : IPlayerFactory
{
    public string Name => "random";

    public BotDifficulty Difficulty => BotDifficulty.Easy;

    public TimeSpan BudgetFor(GameKind kind) => settings.BudgetOverride ?? ArenaSettings.DefaultBudget;

    public Task<IArenaPlayer> CreateAsync(GameKind kind, int seed, CancellationToken ct) =>
        Task.FromResult<IArenaPlayer>(new RandomPlayer(new Random(seed)));

    private sealed class RandomPlayer(Random random) : IArenaPlayer
    {
        public Task<GameAction> ChooseAsync(BotActionRequest request, CancellationToken ct)
        {
            var actions = SeatActions.For(request.State, request.Seat);
            if (actions.Count == 0)
                throw new InvalidOperationException("The random player has nothing to choose from.");

            return Task.FromResult(actions[random.Next(actions.Count)].Action);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
