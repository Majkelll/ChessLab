using System.Net;
using System.Net.Http.Json;
using System.Text;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.BotService.Tests;

public class ActionEndpointTests
{
    public static TheoryData<GameKind> AllKinds() => [.. Enum.GetValues<GameKind>()];

    [Theory]
    [MemberData(nameof(AllKinds))]
    public async Task The_service_answers_an_action_the_rules_accept(GameKind kind)
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);
        var session = Requests.StartedSession(kind);
        var request = Requests.ForActiveSeat(session);

        var response = await service.PostActionAsync(request);
        var action = await response.Content.ReadFromJsonAsync<GameAction>(BotServiceJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        session.Apply(action!, request.Seat, Requests.Now);
    }

    [Fact]
    public async Task A_card_chess_move_is_one_of_the_available_moves()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);
        var request = Requests.ForActiveSeat(Requests.StartedSession(GameKind.CardChess));

        var action = await (await service.PostActionAsync(request)).Content.ReadFromJsonAsync<GameAction>(BotServiceJson.Options);

        Assert.Equal(GameActionKind.Move, action!.Kind);
        Assert.Contains(request.State.AvailableMoves, move => move.From == action.From && move.To == action.To);
    }

    [Fact]
    public async Task A_malformed_body_is_answered_400()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);

        var response = await service.Http.PostAsync("/v1/action",
            new StringContent("{\"kind\":\"AliceChess\",\"seat\":", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_body_without_a_state_is_answered_400()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);

        var response = await service.Http.PostAsync("/v1/action", new StringContent(
            "{\"kind\":\"AliceChess\",\"seat\":{\"side\":\"White\",\"role\":\"Player\"},\"difficulty\":\"Easy\"}",
            Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_state_of_another_kind_is_answered_400()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);
        var request = Requests.ForActiveSeat(Requests.StartedSession(GameKind.MartianChess)) with { Kind = GameKind.AliceChess };

        var response = await service.PostActionAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_finished_game_is_answered_422()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);
        var session = Requests.StartedSession(GameKind.AliceChess);
        session.Game!.Resign(Side.Black);

        var response = await service.PostActionAsync(Requests.ForActiveSeat(session));

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }

    [Fact]
    public async Task A_seat_that_is_not_to_act_is_answered_422()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);
        var request = Requests.ForActiveSeat(Requests.StartedSession(GameKind.CardChess));

        var response = await service.PostActionAsync(request with { Seat = new SeatId(Side.Black, SeatRole.Player) });

        Assert.Equal((HttpStatusCode)422, response.StatusCode);
    }
}
