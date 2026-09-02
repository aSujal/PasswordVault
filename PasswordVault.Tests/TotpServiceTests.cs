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

        var code = TotpService.GenerateCode(RfcSecret, at: time);

        Assert.Equal("287082", code);
    }

    [Fact]
    public void GenerateCode_IsStableWithinSameTimeStep()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(100).UtcDateTime;

        var code1 = TotpService.GenerateCode(RfcSecret, at: time);
        var code2 = TotpService.GenerateCode(RfcSecret, at: time.AddSeconds(5));

        Assert.Equal(code1, code2);
    }

    [Fact]
    public void GenerateCode_ChangesAcrossTimeStep()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(0).UtcDateTime;

        var code1 = TotpService.GenerateCode(RfcSecret, at: time);
        var code2 = TotpService.GenerateCode(RfcSecret, at: time.AddSeconds(30));

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

        var remaining = TotpService.GetSecondsRemaining(at: time);

        Assert.Equal(15, remaining);
    }

    // RFC 6238 Appendix B test vector for SHA256: ASCII secret "12345678901234567890123456789012"
    // (Base32: GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZA), at T=59s the reference
    // 8-digit code is 46119246 -> last 6 digits are 119246. Regression test for accounts (e.g.
    // banks) whose otpauth URI specifies algorithm=SHA256 instead of the SHA1 default.
    [Fact]
    public void GenerateCode_MatchesRfc6238Sha256TestVector()
    {
        var time = DateTimeOffset.FromUnixTimeSeconds(59).UtcDateTime;

        var code = TotpService.GenerateCode(
            "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQGEZA",
            algorithm: "SHA256",
            at: time);

        Assert.Equal("119246", code);
    }

    [Fact]
    public void ParseSecretInput_BareSecret_NormalizesAndUsesDefaults()
    {
        var parsed = TotpService.ParseSecretInput(" gezdgnbvgy3tqojq gezdgnbvgy3tqojq ");

        Assert.Equal(RfcSecret, parsed.Secret);
        Assert.Equal("SHA1", parsed.Algorithm);
        Assert.Equal(6, parsed.Digits);
        Assert.Equal(30, parsed.Period);
    }

    [Fact]
    public void ParseSecretInput_OtpAuthUri_ExtractsSecretAndAlgorithm()
    {
        var uri = "otpauth://totp/FinanzOnline:user?secret=" + RfcSecret + "&issuer=FinanzOnline&algorithm=SHA256&digits=6&period=30";

        var parsed = TotpService.ParseSecretInput(uri);

        Assert.Equal(RfcSecret, parsed.Secret);
        Assert.Equal("SHA256", parsed.Algorithm);
        Assert.Equal(6, parsed.Digits);
        Assert.Equal(30, parsed.Period);
    }
}
