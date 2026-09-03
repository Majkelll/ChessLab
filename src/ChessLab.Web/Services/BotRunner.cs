using System.Collections.Concurrent;
using ChessLab.Bots;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;
using ChessLab.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

/// <summary>
/// Drives bot-occupied seats: whenever the active seat of a room's game is a bot, plays its turn
/// (Brain announcement or Hand move) and broadcasts the result, chaining through consecutive bot turns.
/// </summary>
public sealed class BotRunner(RoomRegistry registry, IHubContext<GameHub> hub, IConfiguration configuration, ILogger<BotRunner> logger)
    : IAsyncDisposable
{
    private static readonly TimeSpan MinThinkDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan MaxThinkDelay = TimeSpan.FromMilliseconds(900);

    private readonly ConcurrentDictionary<string, StockfishEngine> engines = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> roomLocks = new();

    /// <summary>Fire-and-forget: processes any pending bot turns for the room in the background.</summary>
    public void ScheduleBotTurns(string code) => _ = RunPendingBotTurnsAsync(code);

    private async Task RunPendingBotTurnsAsync(string code)
    {
        var roomLock = roomLocks.GetOrAdd(code, static _ => new SemaphoreSlim(1, 1));
        await roomLock.WaitAsync();
        try
        {
            GameSession session;
            try
            {
                session = registry.Get(code);
            }
            catch (HubException)
            {
                return;
            }

            while (session.Game is { IsGameOver: false } game)
            {
                var occupant = session.Room.Seats[session.ActiveSeat];
                if (occupant.Kind != OccupantKind.Bot)
                    return;

                await Task.Delay(Random.Shared.Next((int)MinThinkDelay.TotalMilliseconds, (int)MaxThinkDelay.TotalMilliseconds));

                var engine = await GetOrCreateEngineAsync(code);
                var bot = new HandBrainBot(engine);

                if (game.Phase == TurnPhase.BrainSelecting)
                {
                    var kind = await bot.ChooseBrainAnnouncementAsync(game, occupant.Difficulty!.Value);
                    game.SelectPieceKind(kind);
                }
                else
                {
                    var move = await bot.ChooseHandMoveAsync(game, occupant.Difficulty!.Value);
                    session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);
                }

                await hub.Clients.Group(code).SendAsync("GameUpdated", GameDtoMapper.ToGameUpdateDto(session));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bot turn processing failed for room {Code}.", code);
        }
        finally
        {
            roomLock.Release();
        }
    }

    /// <summary>Fire-and-forget: processes any pending Card Chess bot turns for the room in the background.</summary>
    public void ScheduleCardChessBotTurns(string code) => _ = RunPendingCardChessBotTurnsAsync(code);

    private async Task RunPendingCardChessBotTurnsAsync(string code)
    {
        var roomLock = roomLocks.GetOrAdd(code, static _ => new SemaphoreSlim(1, 1));
        await roomLock.WaitAsync();
        try
        {
            CardChessSession session;
            try
            {
                session = registry.GetCardChess(code);
            }
            catch (HubException)
            {
                return;
            }

            while (session.Game is { IsGameOver: false } game)
            {
                var occupant = session.Room.Seats[session.ActiveSeat];
                if (occupant.Kind != OccupantKind.Bot)
                    return;

                await Task.Delay(Random.Shared.Next((int)MinThinkDelay.TotalMilliseconds, (int)MaxThinkDelay.TotalMilliseconds));

                var engine = await GetOrCreateEngineAsync(code);
                var bot = new CardChessBot(engine);
                var move = await bot.ChooseMoveAsync(game, occupant.Difficulty!.Value);
                session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);

                await hub.Clients.Group(code).SendAsync("CardChessGameUpdated", GameDtoMapper.ToCardChessUpdateDto(session));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Card Chess bot turn processing failed for room {Code}.", code);
        }
        finally
        {
            roomLock.Release();
        }
    }

    /// <summary>Fire-and-forget: processes any pending Arcane Chess bot turns for the room in the background.</summary>
    public void ScheduleArcaneChessBotTurns(string code) => _ = RunPendingArcaneChessBotTurnsAsync(code);

    private async Task RunPendingArcaneChessBotTurnsAsync(string code)
    {
        var roomLock = roomLocks.GetOrAdd(code, static _ => new SemaphoreSlim(1, 1));
        await roomLock.WaitAsync();
        try
        {
            ArcaneChessSession session;
            try
            {
                session = registry.GetArcaneChess(code);
            }
            catch (HubException)
            {
                return;
            }

            while (session.Game is { IsGameOver: false } game)
            {
                var occupant = session.Room.Seats[session.ActiveSeat];
                if (occupant.Kind != OccupantKind.Bot)
                    return;

                await Task.Delay(Random.Shared.Next((int)MinThinkDelay.TotalMilliseconds, (int)MaxThinkDelay.TotalMilliseconds));

                var engine = await GetOrCreateEngineAsync(code);
                var bot = new ArcaneChessBot(engine);

                var spellCast = bot.ChooseSpell(game, game.SideToMove);
                if (spellCast is { } cast)
                {
                    try { session.CastSpell(cast.Spell, cast.Target); }
                    catch (InvalidOperationException) { /* heuristic guessed wrong; just move instead */ }
                }

                var move = await bot.ChooseMoveAsync(game, occupant.Difficulty!.Value);
                session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);

                await hub.Clients.Group(code).SendAsync("ArcaneChessGameUpdated", GameDtoMapper.ToArcaneChessUpdateDto(session));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Arcane Chess bot turn processing failed for room {Code}.", code);
        }
        finally
        {
            roomLock.Release();
        }
    }

    private async Task<StockfishEngine> GetOrCreateEngineAsync(string code)
    {
        if (engines.TryGetValue(code, out var existing))
            return existing;

        var path = configuration["Bots:StockfishPath"] ?? "stockfish";
        var engine = await StockfishEngine.StartAsync(path);
        engines[code] = engine;
        return engine;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var engine in engines.Values)
            await engine.DisposeAsync();
    }
}
