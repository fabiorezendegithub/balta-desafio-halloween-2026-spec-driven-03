using System.Security.Cryptography;
using System.Text;

namespace Encurtador.Application.Links;

public static class ManagementKey
{
    private const int KeyByteLength = 32;
    private const int KeyLength = 43;

    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(KeyByteLength);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Hash(string key)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public static bool Verify(string presentedKey, string storedHash)
    {
        var presented = Encoding.UTF8.GetBytes(Hash(presentedKey));
        var stored = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(presented, stored);
    }

    public static bool IsWellFormed(string key) =>
        key.Length == KeyLength && key.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
