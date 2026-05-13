using System.Security.Cryptography;
using System.Text;

namespace FactoryGame.Infrastructure.Security;

public static class ApiKeyHash
{
    public static string Sha256Hex(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
