using ChessLab.Bots;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Bots;

public class GreedyBotTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);

    private static ChessMove Move(string from, string to, PieceKind piece = PieceKind.Pawn, PieceKind? captured = null,
        PieceKind? promoteTo = null) =>
        new(Square.Parse(from), Square.Parse(to), piece, captured, promoteTo, false, false, $"{from}{to}");

    private static GameStateEnvelopeDto StateWith(GameKind kind, params ChessMove[] moves) =>
        new(kind, Side.White, "", moves, [], 600_000, 600_000, false, null, null);

    private static async Task<HashSet<GameAction>> ChoicesAsync(IGameBot bot, GameStateEnvelopeDto state,
        BotDifficulty difficulty, int tries = 60)
    {
        var choices = new HashSet<GameAction>();
        for (var i = 0; i < tries; i++)
            choices.Add(await bot.ChooseActionAsync(state, White, difficulty));
        return choices;
    }

    [Fact]
    public async Task BotsWithEqualSeeds_ChooseTheSameMoves()
    {
        var moves = Enumerable.Range(0, 8).Select(file => Move($"{(char)('a' + file)}2", $"{(char)('a' + file)}3")).ToArray();
        var state = StateWith(GameKind.AliceChess, moves);
        var first = new AliceChessBot(seed: 4);
        var second = new AliceChessBot(seed: 4);

        for (var i = 0; i < 20; i++)
            Assert.Equal(await first.ChooseActionAsync(state, White, BotDifficulty.Easy),
                await second.ChooseActionAsync(state, White, BotDifficulty.Easy));
    }

    [Theory]
    [InlineData(BotDifficulty.Hard)]
    [InlineData(BotDifficulty.Expert)]
    public async Task AliceBot_OnHardOrExpert_AlwaysTakesTheMostValuableCapture(BotDifficulty difficulty)
    {
        var state = StateWith(GameKind.AliceChess,
            Move("a2", "a3"), Move("b2", "c3", captured: PieceKind.Knight), Move("d2", "e3", captured: PieceKind.Rook));

        var choices = await ChoicesAsync(new AliceChessBot(), state, difficulty);

        Assert.Equal(GameAction.MovePiece(Square.Parse("d2"), Square.Parse("e3")), Assert.Single(choices));
    }

    [Fact]
    public async Task AliceBot_ScoresAPromotionByThePromotedPiece()
    {
        var state = StateWith(GameKind.AliceChess,
            Move("a2", "b3", captured: PieceKind.Rook), Move("h7", "h8", promoteTo: PieceKind.Queen));

        var choices = await ChoicesAsync(new AliceChessBot(), state, BotDifficulty.Hard);

        Assert.Equal(GameAction.MovePiece(Square.Parse("h7"), Square.Parse("h8"), PieceKind.Queen), Assert.Single(choices));
    }

    [Fact]
    public async Task AliceBot_OnEasy_PlaysAnyAvailableMove()
    {
        var state = StateWith(GameKind.AliceChess, Move("a2", "a3"), Move("d2", "e3", captured: PieceKind.Queen));

        var choices = await ChoicesAsync(new AliceChessBot(), state, BotDifficulty.Easy);

        Assert.Equal(2, choices.Count);
    }

    [Fact]
    public async Task AbsorptionBot_AddsThreeTimesTheCapturedValue()
    {
        var state = StateWith(GameKind.AbsorptionChess,
            Move("a2", "b3", captured: PieceKind.Rook), Move("c2", "d3", captured: PieceKind.Knight, promoteTo: PieceKind.Knight));

        var choices = await ChoicesAsync(new AbsorptionChessBot(), state, BotDifficulty.Hard);

        Assert.Equal(GameAction.MovePiece(Square.Parse("a2"), Square.Parse("b3")), Assert.Single(choices));
    }

    [Fact]
    public async Task MartianBot_AvoidsGivingAPieceAwayAcrossTheCanal()
    {
        var state = StateWith(GameKind.MartianChess,
            Move("a1", "a2", PieceKind.Queen), Move("b3", "b5", PieceKind.Queen));

        var choices = await ChoicesAsync(new MartianChessBot(), state, BotDifficulty.Hard);

        Assert.Equal(GameAction.MovePiece(Square.Parse("a1"), Square.Parse("a2")), Assert.Single(choices));
    }
}
