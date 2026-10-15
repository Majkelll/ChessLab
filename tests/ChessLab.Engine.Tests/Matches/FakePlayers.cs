using ChessLab.Arena.Players;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Matches;

internal sealed class FakePlayer(string name, Func<BotActionRequest, CancellationToken, Task<GameAction>> choose,
    TimeSpan? budget = null) : IPlayerFactory
{
    public string Name => name;

    public BotDifficulty Difficulty => BotDifficulty.Expert;

    public TimeSpan BudgetFor(GameKind kind) => budget ?? TimeSpan.FromMilliseconds(100);

    public Task<IArenaPlayer> CreateAsync(GameKind kind, int seed, CancellationToken ct) =>
        Task.FromResult<IArenaPlayer>(new Player(choose));

    public static FakePlayer Illegal() => new("illegal", (_, _) =>
        Task.FromResult(GameAction.MovePiece(Square.Parse("a1"), Square.Parse("h8"))));

    public static FakePlayer Crashing() => new("crashing", (_, _) =>
        throw new NullReferenceException("The engine fell over."));

    public static FakePlayer Slow() => new("slow", async (request, ct) =>
    {
        await Task.Delay(TimeSpan.FromMilliseconds(120), CancellationToken.None);
        return FirstMove(request);
    }, TimeSpan.FromMilliseconds(40));

    public static FakePlayer Hanging() => new("hanging", async (_, ct) =>
    {
        await Task.Delay(Timeout.Infinite, ct);
        throw new InvalidOperationException("Unreachable.");
    }, TimeSpan.FromMilliseconds(20));

    public static FakePlayer First() => new("first", (request, _) => Task.FromResult(FirstMove(request)));

    private static GameAction FirstMove(BotActionRequest request)
    {
        var move = request.State.AvailableMoves[0];
        return GameAction.MovePiece(move.From, move.To, move.PromoteTo);
    }

    private sealed class Player(Func<BotActionRequest, CancellationToken, Task<GameAction>> choose) : IArenaPlayer
    {
        public Task<GameAction> ChooseAsync(BotActionRequest request, CancellationToken ct) => choose(request, ct);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
