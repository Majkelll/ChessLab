using System.Security.Cryptography;
using System.Text;

namespace ChessLab.BotService;

public sealed class ApiKey(string key)
{
    private const string Scheme = "Bearer ";

    private readonly byte[] expectedHash = Hash(key);

    public bool Authorizes(string authorizationHeader)
    {
        if (!authorizationHeader.StartsWith(Scheme, StringComparison.Ordinal))
            return false;

        return CryptographicOperations.FixedTimeEquals(Hash(authorizationHeader[Scheme.Length..]), expectedHash);
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
