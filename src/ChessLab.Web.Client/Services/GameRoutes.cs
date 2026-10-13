using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.Components;

namespace ChessLab.Web.Client.Services;

public static class GameRoutes
{
    public static string PathFor(GameKind kind, string code) => kind switch
    {
        GameKind.CardChess => $"/cardchess/{code}",
        GameKind.ArcaneChess => $"/arcanechess/{code}",
        GameKind.BiddingChess => $"/bidding/{code}",
        GameKind.ProgressiveChess => $"/progressive/{code}",
        GameKind.AliceChess => $"/alice/{code}",
        GameKind.AbsorptionChess => $"/absorption/{code}",
        GameKind.MartianChess => $"/martian/{code}",
        GameKind.DraftChess => $"/draft/{code}",
        _ => $"/game/{code}",
    };

    public static bool RedirectIfNotThisRoomsPage(NavigationManager navigation, RoomStateDto room)
    {
        var expected = room is { HasStarted: false, IsLocked: false } ? $"/room/{room.Code}" : PathFor(room.Kind, room.Code);
        var current = "/" + navigation.ToBaseRelativePath(navigation.Uri).Split('?', '#')[0];
        if (string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            return false;

        navigation.NavigateTo(expected);
        return true;
    }
}
