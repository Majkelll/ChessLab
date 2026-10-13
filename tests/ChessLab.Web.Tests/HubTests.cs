using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using ChessLab.Web.Client.Services;
using ChessLab.Web.Hubs;
using ChessLab.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChessLab.Web.Tests;

[Collection(HubAppCollection.Name)]
public class HubTests(HubApp app)
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);

    [Fact]
    public async Task A_seat_rejection_reaches_the_caller_with_the_rules_message()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.CardChess);

        var rejection = await Assert.ThrowsAsync<HubException>(() =>
            guest.Connection.InvokeAsync("ClaimSeat", code, White));

        Assert.Equal("Seat is already taken.", HubRejection.MessageOf(rejection));
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task A_clock_rejection_reaches_the_caller_without_the_parameter_suffix()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.CardChess);

        var rejection = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync("SetClockSettings", room.Code, 0, 0));

        Assert.Equal("Initial clock must be positive.", HubRejection.MessageOf(rejection));
    }

    [Fact]
    public async Task A_seat_outside_the_rooms_layout_is_rejected()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.HandAndBrain);

        var rejection = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync("ClearSeat", room.Code, White));

        Assert.Equal("Seat White-Player does not exist in this room.", HubRejection.MessageOf(rejection));
    }

    [Fact]
    public async Task Only_the_host_can_set_bots_and_clear_seats()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.CardChess);

        var bot = await Assert.ThrowsAsync<HubException>(() =>
            guest.Connection.InvokeAsync("SetSeatBot", code, White, BotDifficulty.Easy));
        var clear = await Assert.ThrowsAsync<HubException>(() =>
            guest.Connection.InvokeAsync("ClearSeat", code, White));
        await host.Connection.InvokeAsync("ClearSeat", code, Black);

        Assert.Equal("Only the room's host can add or change a bot.", HubRejection.MessageOf(bot));
        Assert.Equal("Only the room's host can clear a seat.", HubRejection.MessageOf(clear));
        var state = await host.Connection.InvokeAsync<RoomStateDto?>("JoinRoom", code);
        Assert.Equal(OccupantKind.Empty, state!.Seats.Single(seat => seat.Id == Black).Kind);
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task Starting_a_room_with_an_empty_seat_is_rejected_with_the_rules_message()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.CardChess);

        var rejection = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync("StartGame", room.Code));

        Assert.Equal("All seats must be filled before the room can be locked.", HubRejection.MessageOf(rejection));
    }

    [Fact]
    public async Task Each_Arcane_Chess_player_receives_only_their_own_hands()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.ArcaneChess);
        var whiteCopy = host.NextAsync("GameStarted");
        var blackCopy = guest.NextAsync("GameStarted");

        await host.Connection.InvokeAsync("StartGame", code);
        var white = await whiteCopy;
        var black = await blackCopy;

        Assert.Equal(3, white.Arcane!.WhiteSpellHand.Count);
        Assert.Empty(white.Arcane.BlackSpellHand);
        Assert.Empty(white.CardChess!.BlackHand);
        Assert.NotEmpty(white.AvailableMoves);
        Assert.Equal(3, black.Arcane!.BlackSpellHand.Count);
        Assert.Empty(black.Arcane.WhiteSpellHand);
        Assert.Empty(black.CardChess!.WhiteHand);
        Assert.Empty(black.AvailableMoves);
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task A_spectator_of_Arcane_Chess_is_sent_no_hands()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.ArcaneChess);
        await host.Connection.InvokeAsync("StartGame", code);
        await using var spectator = await app.SignInAsync("Cezary");
        await spectator.Connection.InvokeAsync<RoomStateDto?>("JoinRoom", code);

        var state = await spectator.Connection.InvokeAsync<GameStateEnvelopeDto>("GetGameState", code);

        Assert.Empty(state.Arcane!.WhiteSpellHand);
        Assert.Empty(state.Arcane.BlackSpellHand);
        Assert.Empty(state.CardChess!.WhiteHand);
        Assert.Empty(state.CardChess.BlackHand);
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task Acting_or_resigning_after_the_game_ended_is_rejected()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.AliceChess);
        await host.Connection.InvokeAsync("StartGame", code);
        await guest.Connection.InvokeAsync("Resign", code);

        var move = GameAction.MovePiece(Square.Parse("e2"), Square.Parse("e4"));
        var action = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync("PerformAction", code, move));
        var resign = await Assert.ThrowsAsync<HubException>(() => host.Connection.InvokeAsync("Resign", code));

        Assert.Equal("The game has already ended.", HubRejection.MessageOf(action));
        Assert.Equal("The game has already ended.", HubRejection.MessageOf(resign));
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task A_finished_game_is_archived_and_found_by_room_code_only_by_its_players()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.AliceChess);
        await host.Connection.InvokeAsync("StartGame", code);
        await guest.Connection.InvokeAsync("Resign", code);
        await using var outsider = await app.SignInAsync("Cezary");

        var mine = await host.Connection.InvokeAsync<IReadOnlyList<GameHistoryEntryDto>>("GetMyGameHistory", 50);
        var byCode = await host.Connection.InvokeAsync<GameHistoryDetailDto?>("GetGameHistoryByRoomCode", code);
        var forOutsider = await outsider.Connection.InvokeAsync<GameHistoryDetailDto?>("GetGameHistoryByRoomCode", code);
        var byId = await outsider.Connection.InvokeAsync<GameHistoryDetailDto?>("GetGameHistoryEntry", mine[0].Id);

        var entry = Assert.Single(mine);
        Assert.Equal(GameEndReason.Resignation, entry.EndReason);
        Assert.Equal(Side.White, entry.Winner);
        Assert.Equal(code, byCode!.Summary.RoomCode);
        Assert.Null(forOutsider);
        Assert.NotNull(byId);
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task A_user_cannot_host_more_open_rooms_than_the_limit()
    {
        await using var host = await app.SignInAsync("Ala");
        for (var i = 0; i < RoomRegistry.OpenRoomsPerHost; i++)
            await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.CardChess);

        var rejection = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.CardChess));

        Assert.Equal("You already host 5 open rooms. Wait for one to finish or expire.", HubRejection.MessageOf(rejection));
    }

    [Fact]
    public async Task A_connection_that_calls_too_often_is_slowed_down()
    {
        await using var caller = await app.SignInAsync("Ala");
        for (var i = 0; i < HubRateLimitFilter.CallsPerWindow; i++)
            await caller.Connection.InvokeAsync<RoomStateDto?>("JoinRoom", "NOROOM");

        var rejection = await Assert.ThrowsAsync<HubException>(() =>
            caller.Connection.InvokeAsync<RoomStateDto?>("JoinRoom", "NOROOM"));

        Assert.Equal("Too many requests. Slow down.", HubRejection.MessageOf(rejection));
    }

    [Fact]
    public async Task Bots_play_a_room_that_has_no_human_seated()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.AliceChess);
        await host.Connection.InvokeAsync("SetSeatBot", room.Code, White, BotDifficulty.Easy);
        await host.Connection.InvokeAsync("SetSeatBot", room.Code, Black, BotDifficulty.Easy);
        var secondMove = host.NextAsync("GameUpdated", update => update.MoveHistoryCount >= 2);

        await host.Connection.InvokeAsync("StartGame", room.Code);

        Assert.True((await secondMove).MoveHistoryCount >= 2);
    }
}

public class ClockWatchdogTests(ShortClockHubApp app) : IClassFixture<ShortClockHubApp>
{
    [Fact]
    public async Task A_game_nobody_plays_ends_by_timeout_for_the_side_to_move()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.AliceChess);
        var ending = host.NextAsync("GameUpdated", update => update.IsGameOver);

        await host.Connection.InvokeAsync("StartGame", code);
        var update = await ending;

        Assert.Equal(GameEndReason.Timeout, update.EndReason);
        Assert.Equal(Side.Black, update.Winner);
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }

    [Fact]
    public async Task An_action_sent_after_the_time_ran_out_is_not_played()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.AliceChess);
        await host.Connection.InvokeAsync("StartGame", code);
        var session = ((RoomRegistry)app.Services.GetService(typeof(RoomRegistry))!).Get(code);
        await Task.Delay(TimeSpan.FromMilliseconds(1100));

        try
        {
            await host.Connection.InvokeAsync(
                "PerformAction", code, GameAction.MovePiece(Square.Parse("e2"), Square.Parse("e4")));
        }
        catch (HubException)
        {
        }

        Assert.True(session.Game!.IsGameOver);
        Assert.Empty(session.Game.MoveNotations);
        await host.DisposeAsync();
        await guest.DisposeAsync();
    }
}
