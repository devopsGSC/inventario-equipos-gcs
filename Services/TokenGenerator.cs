using System.Security.Cryptography;

namespace InventarioTI.Services;

public static class TokenGenerator
{
    // Token aleatorio de 256 bits, URL-safe, para links de un solo uso.
    public static string GenerarTokenUrlSafe() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
