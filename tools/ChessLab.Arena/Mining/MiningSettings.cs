using System.Text.Json;

namespace ChessLab.Arena.Mining;

public sealed record MiningSettings(int Threshold = 200, int Depth = 2)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static MiningSettings Load(string path) =>
        JsonSerializer.Deserialize<MiningSettings>(File.ReadAllText(path), Json)
        ?? throw new FormatException($"{path} is empty.");
}
