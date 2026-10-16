using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Client.Services;

public static class HubRejection
{
    private const string ServerMessageMarker = "HubException: ";

    public static string MessageOf(HubException rejection)
    {
        var markerStart = rejection.Message.IndexOf(ServerMessageMarker, StringComparison.Ordinal);
        return markerStart < 0 ? rejection.Message : rejection.Message[(markerStart + ServerMessageMarker.Length)..];
    }
}
