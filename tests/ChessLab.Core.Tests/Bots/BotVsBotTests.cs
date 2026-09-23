using ChessLab.Bots;
using ChessLab.Core.Rooms;
using Xunit.Sdk;

namespace ChessLab.Core.Tests.Bots;

/// <summary>The modes whose bots don't need Stockfish play each other here, start to finish, so the
/// bots are held to the same bar as the rules: never an illegal action, never a stalled room.</summary>
public class BotVsBotTests
{
    [Theory]
    [InlineData(GameKind.BiddingChess)]
    [InlineData(GameKind.AliceChess)]
    [InlineData(GameKind.AbsorptionChess)]
    [InlineData(GameKind.MartianChess)]
    [InlineData(GameKind.DraftChess)]
    public async Task BotsPlayAWholeGameWithoutGettingStuck(GameKind kind)
    {
        foreach (var difficulty in (BotDifficulty[])[BotDifficulty.Easy, BotDifficulty.Expert])
        {
            var room = new Room("BOTS", Guid.NewGuid(), kind);
            foreach (var seat in room.SeatIds)
                room.SetBot(seat, difficulty);

            var session = NewSession(kind, room);
            session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, DateTimeOffset.UtcNow, new Random(7));

            var bot = GameBots.For(kind, null);
            var actions = 0;

            while (session.HasActiveGame)
            {
                if (actions++ > 4000)
                    throw new XunitException($"{kind} on {difficulty}: the bots never finished ({session.Game!.PositionText}).");

                foreach (var seat in session.ActiveSeats)
                {
                    var action = await bot.ChooseActionAsync(session, seat, difficulty);
                    session.Apply(action, seat, DateTimeOffset.UtcNow);

                    if (!session.HasActiveGame)
                        break;
                }
            }

            Assert.True(session.Game!.IsGameOver);
            Assert.NotNull(session.Game.EndResult);
        }
    }

    private static IRoomSession NewSession(GameKind kind, Room room) => kind switch
    {
        GameKind.BiddingChess => new BiddingChessSession(room),
        GameKind.AliceChess => new AliceChessSession(room),
        GameKind.AbsorptionChess => new AbsorptionChessSession(room),
        GameKind.MartianChess => new MartianChessSession(room),
        GameKind.DraftChess => new DraftChessSession(room),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "This test only covers the engine-free bots."),
    };
}
