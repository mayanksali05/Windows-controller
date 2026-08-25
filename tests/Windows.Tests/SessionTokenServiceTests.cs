using WinLock.Service.Authentication;
using Xunit;

namespace Windows.Tests;

public sealed class SessionTokenServiceTests
{
    [Fact]
    public void Issue_Validate_RoundTrips()
    {
        var service = new SessionTokenService(TimeSpan.FromMinutes(10));
        var issued = service.Issue("phone-1");

        var validated = service.Validate(issued.Token);

        Assert.NotNull(validated);
        Assert.Equal("phone-1", validated!.DeviceId);
        Assert.Equal(issued.Token, validated.Token);
    }

    [Fact]
    public void TamperedToken_IsRejected()
    {
        var service = new SessionTokenService(TimeSpan.FromMinutes(10));
        var issued = service.Issue("phone-1");

        var tampered = issued.Token[..^1] + (issued.Token[^1] == 'A' ? 'B' : 'A');
        Assert.Null(service.Validate(tampered));
    }

    [Fact]
    public void ExpiredToken_IsRejected()
    {
        var service = new SessionTokenService(TimeSpan.FromSeconds(-1));
        var issued = service.Issue("phone-1");

        Assert.Null(service.Validate(issued.Token));
    }

    [Fact]
    public void MalformedToken_IsRejected()
    {
        var service = new SessionTokenService(TimeSpan.FromMinutes(10));

        Assert.Null(service.Validate("not-a-token"));
        Assert.Null(service.Validate(""));
        Assert.Null(service.Validate("a.b.c"));
    }
}