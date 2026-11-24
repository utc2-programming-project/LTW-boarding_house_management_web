using Microsoft.Extensions.Configuration;
using Web.Features.AccessControl.Services;

namespace NhaTro.UnitTests.Features.AccessControl;

public sealed class GateTokenServiceTests
{
    private static GateTokenService Create() =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AccessControl:QrSigningKey"] = "unit-test-signing-key-0123456789",
            })
            .Build());

    [Fact]
    public void TokensAreRandomAndHashedDeterministically()
    {
        var service = Create();
        var first = service.NewToken();
        var second = service.NewToken();

        Assert.NotEqual(first, second);
        Assert.NotEqual(first, service.Hash(first));
        Assert.Equal(service.Hash(first), service.Hash(first));
    }

    [Fact]
    public void SixDigitCodeHasFixedLength()
    {
        var service = Create();
        for (var i = 0; i < 25; i++)
        {
            var code = service.NewCode();
            Assert.Equal(6, code.Length);
            Assert.True(code.All(char.IsDigit));
        }
    }

    [Fact]
    public void FixedTimeEqualsMatchesOnlyIdenticalValues()
    {
        var service = Create();
        Assert.True(service.FixedTimeEquals("abc", "abc"));
        Assert.False(service.FixedTimeEquals("abc", "abd"));
        Assert.False(service.FixedTimeEquals("abc", "abcd"));
    }

    [Fact]
    public void ResidentTokenVerifiesInsideWindowAndRejectsStaleToken()
    {
        var service = Create();
        var now = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
        var token = service.IssueResidentToken("user-1", 7, now);

        Assert.True(service.TryVerifyResidentToken(token, now.AddSeconds(30), out var userId, out var propertyId));
        Assert.Equal("user-1", userId);
        Assert.Equal(7, propertyId);

        Assert.False(service.TryVerifyResidentToken(token, now.AddMinutes(5), out _, out _));
    }

    [Fact]
    public void ResidentTokenRejectsTampering()
    {
        var service = Create();
        var now = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
        var token = service.IssueResidentToken("user-1", 7, now);
        var tampered = token.Replace(".7.", ".8.");

        Assert.False(service.TryVerifyResidentToken(tampered, now, out _, out _));
    }

    [Fact]
    public void TotpCodeVerifiesWithinOneWindow()
    {
        var secret = "12345678901234567890"u8.ToArray();
        var now = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
        var code = GateTokenService.ComputeTotp(secret, now);

        Assert.True(GateTokenService.VerifyTotp(secret, code, now.AddSeconds(60)));
        Assert.False(GateTokenService.VerifyTotp(secret, code, now.AddSeconds(180)));
        Assert.False(GateTokenService.VerifyTotp(secret, "000000", now.AddSeconds(300)));
    }
}
