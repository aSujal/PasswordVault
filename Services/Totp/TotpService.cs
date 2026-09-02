using System;
using System.Security.Cryptography;

namespace PasswordVault.Services.Totp;

public record TotpParameters(string Secret, string Algorithm, int Digits, int Period);

public static class TotpService
{
    public const string DefaultAlgorithm = "SHA1";
    public const int DefaultDigits = 6;
    public const int DefaultPeriod = 30;

    public static bool IsValidSecret(string secret)
    {
        try
        {
            return Base32Decode(secret).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    // Accepts either a bare Base32 secret or a full "otpauth://totp/..." URI (as produced by
    // most 2FA QR codes), extracting algorithm/digits/period so accounts that don't use the
    // SHA1/6-digit/30s defaults (e.g. some banks use SHA256) generate matching codes.
    public static TotpParameters ParseSecretInput(string input)
    {
        string trimmed = input.Trim();
        if (!trimmed.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            return new TotpParameters(NormalizeSecret(trimmed), DefaultAlgorithm, DefaultDigits, DefaultPeriod);

        int queryStart = trimmed.IndexOf('?');
        string query = queryStart >= 0 ? trimmed[(queryStart + 1)..] : string.Empty;

        string? secret = null;
        string algorithm = DefaultAlgorithm;
        int digits = DefaultDigits;
        int period = DefaultPeriod;

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length != 2) continue;

            string key = Uri.UnescapeDataString(kv[0]);
            string value = Uri.UnescapeDataString(kv[1]);
            switch (key.ToLowerInvariant())
            {
                case "secret": secret = value; break;
                case "algorithm": algorithm = value.ToUpperInvariant(); break;
                case "digits": if (int.TryParse(value, out var d)) digits = d; break;
                case "period": if (int.TryParse(value, out var p)) period = p; break;
            }
        }

        if (string.IsNullOrWhiteSpace(secret))
            throw new FormatException("otpauth URI is missing a 'secret' parameter.");

        return new TotpParameters(NormalizeSecret(secret), algorithm, digits, period);
    }

    public static string GenerateCode(
        string base32Secret,
        string algorithm = DefaultAlgorithm,
        int digits = DefaultDigits,
        int period = DefaultPeriod,
        DateTime? at = null)
    {
        byte[] key = Base32Decode(base32Secret);
        long counter = ToUnixTimeSeconds(at ?? DateTime.UtcNow) / Math.Max(1, period);

        byte[] counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using HMAC hmac = CreateHmac(algorithm, key);
        byte[] hash = hmac.ComputeHash(counterBytes);

        int offset = hash[^1] & 0x0F;
        int binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        int code = binary % (int)Math.Pow(10, digits);
        return code.ToString().PadLeft(digits, '0');
    }

    public static int GetSecondsRemaining(int period = DefaultPeriod, DateTime? at = null)
    {
        int p = Math.Max(1, period);
        long seconds = ToUnixTimeSeconds(at ?? DateTime.UtcNow);
        return p - (int)(seconds % p);
    }

    private static HMAC CreateHmac(string algorithm, byte[] key) => algorithm.ToUpperInvariant() switch
    {
        "SHA256" => new HMACSHA256(key),
        "SHA512" => new HMACSHA512(key),
        _ => new HMACSHA1(key),
    };

    private static string NormalizeSecret(string secret) =>
        secret.Trim().Replace(" ", "").ToUpperInvariant();

    private static long ToUnixTimeSeconds(DateTime dt) =>
        new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeSeconds();

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        string cleaned = input.Trim().TrimEnd('=').Replace(" ", "").ToUpperInvariant();
        if (cleaned.Length == 0)
            throw new FormatException("Empty Base32 secret.");

        int bitBuffer = 0, bitCount = 0;
        var output = new System.Collections.Generic.List<byte>();

        foreach (char c in cleaned)
        {
            int value = alphabet.IndexOf(c);
            if (value < 0)
                throw new FormatException($"Invalid Base32 character: {c}");

            bitBuffer = (bitBuffer << 5) | value;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bitCount -= 8;
                output.Add((byte)((bitBuffer >> bitCount) & 0xFF));
            }
        }

        return [.. output];
    }
}
