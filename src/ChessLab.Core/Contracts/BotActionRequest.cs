using System.Text.Json;
using System.Text.Json.Serialization;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Contracts;

public sealed record BotActionRequest(
    GameKind Kind,
    SeatId Seat,
    BotDifficulty Difficulty,
    GameStateEnvelopeDto State);

public sealed record BotServiceHealthDto(string Version, int Workers);

public sealed record BotServiceQueueDto(int Queued, int Running);

public static class BotServiceJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };
}
