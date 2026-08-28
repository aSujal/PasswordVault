using System;
using System.Security.Cryptography;

namespace PasswordVault.Services.Totp;

public static class TotpService
{
    private const int StepSeconds = 30;
    private const int Digits = 6;

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

    public static string GenerateCode(string base32Secret, DateTime? at = null)
    {
        byte[] key = Base32Decode(base32Secret);
        long counter = ToUnixTimeSeconds(at ?? DateTime.UtcNow) / StepSeconds;

        byte[] counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        byte[] hash = hmac.ComputeHash(counterBytes);

        int offset = hash[^1] & 0x0F;
        int binary =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        int code = binary % (int)Math.Pow(10, Digits);
        return code.ToString().PadLeft(Digits, '0');
    }

    public static int GetSecondsRemaining(DateTime? at = null)
    {
        long seconds = ToUnixTimeSeconds(at ?? DateTime.UtcNow);
        return StepSeconds - (int)(seconds % StepSeconds);
    }

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
