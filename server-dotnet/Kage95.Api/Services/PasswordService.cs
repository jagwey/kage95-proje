using System.Security.Cryptography;
using System.Text;
using Kage95.Api.Middleware;
using Org.BouncyCastle.Crypto.Generators;

namespace Kage95.Api.Services;

/// <summary>
/// Node.js server/auth.mjs içindeki scrypt (N=131072, r=8, p=1, dkLen=32, format=hexSalt:hexHash)
/// yapısıyla bit-seviyesinde uyumlu şifre servisi.
/// </summary>
public class PasswordService
{
    private const int ScryptN = 131072;
    private const int ScryptR = 8;
    private const int ScryptP = 1;
    private const int DerivedKeyLength = 32;

    private readonly SemaphoreSlim _gate = new(2, 2);

    public async Task<string> HashPasswordAsync(string password)
    {
        var saltHex = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var derived = await DeriveAsync(password, saltHex);
        return $"{saltHex}:{Convert.ToHexStringLower(derived)}";
    }

    public async Task<bool> VerifyPasswordAsync(string password, string? storedHash)
    {
        var parts = storedHash?.Split(':');
        var validFormat = parts is { Length: 2 } && parts[0].Length > 0 && parts[1].Length == DerivedKeyLength * 2;
        var saltHex = validFormat ? parts![0] : new string('0', 32);
        var expectedBytes = validFormat ? TryFromHex(parts![1]) : new byte[DerivedKeyLength];
        if (expectedBytes is null || expectedBytes.Length != DerivedKeyLength)
        {
            expectedBytes = new byte[DerivedKeyLength];
            validFormat = false;
        }

        var actual = await DeriveAsync(password, saltHex);
        return CryptographicOperations.FixedTimeEquals(actual, expectedBytes) && validFormat;
    }

    private async Task<byte[]> DeriveAsync(string password, string saltUtf8)
    {
        if (!await _gate.WaitAsync(TimeSpan.FromSeconds(5)))
            throw new ApiException(StatusCodes.Status503ServiceUnavailable, "Lütfen biraz sonra tekrar dene.");

        try
        {
            return await Task.Run(() => SCrypt.Generate(
                Encoding.UTF8.GetBytes(password),
                Encoding.UTF8.GetBytes(saltUtf8),
                ScryptN,
                ScryptR,
                ScryptP,
                DerivedKeyLength
            ));
        }
        finally
        {
            _gate.Release();
        }
    }

    private static byte[]? TryFromHex(string hex)
    {
        try { return Convert.FromHexString(hex); }
        catch { return null; }
    }
}
