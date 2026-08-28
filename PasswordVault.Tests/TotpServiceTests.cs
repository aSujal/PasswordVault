using System;
using PasswordVault.Services.Totp;
using Xunit;

namespace PasswordVault.Tests.Services;

public class TotpServiceTests
{
    // RFC 6238 Appendix B test vector: ASCII secret "12345678901234567890" (Base32: GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ),
    // SHA-1, at Unix time T=59s the reference 8-digit code is 94287082 -> last 6 digits are 287082.
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    [Fact]
    public void GenerateCode_MatchesRfc6238TestVector()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(59).UtcDateTime;

        var code = TotpService.GenerateCode(RfcSecret, time);

        Assert.Equal("287082", code);
    }

    [Fact]
    public void GenerateCode_IsStableWithinSameTimeStep()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(100).UtcDateTime;

        var code1 = TotpService.GenerateCode(RfcSecret, time);
        var code2 = TotpService.GenerateCode(RfcSecret, time.AddSeconds(5));

        Assert.Equal(code1, code2);
    }

    [Fact]
    public void GenerateCode_ChangesAcrossTimeStep()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(0).UtcDateTime;

        var code1 = TotpService.GenerateCode(RfcSecret, time);
        var code2 = TotpService.GenerateCode(RfcSecret, time.AddSeconds(30));

        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public void IsValidSecret_RejectsInvalidBase32()
    {
        Assert.False(TotpService.IsValidSecret("not-valid-base32!!!"));
        Assert.False(TotpService.IsValidSecret(""));
    }

    [Fact]
    public void IsValidSecret_AcceptsValidBase32()
    {
        Assert.True(TotpService.IsValidSecret(RfcSecret));
    }

    [Fact]
    public void GetSecondsRemaining_IsWithinStepWindow()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(45).UtcDateTime; // 15s into the 30s step

        var remaining = TotpService.GetSecondsRemaining(time);

        Assert.Equal(15, remaining);
    }
}
