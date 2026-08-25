using WinLock.Service.Pairing;
using Xunit;

namespace Windows.Tests;

public sealed class PairingSessionServiceTests
{
    [Fact]
    public void Create_ReturnsTokenAndNonce_AndActive()
    {
        var service = new PairingSessionService(TimeSpan.FromMinutes(5));
        var session = service.Create();

        Assert.False(string.IsNullOrEmpty(session.Token));
        Assert.False(string.IsNullOrEmpty(session.Nonce));
        Assert.NotEqual(session.Token, session.Nonce);
        Assert.False(session.IsExpired);
        Assert.Same(session, service.GetActive());
    }

    [Fact]
    public void Token_IsSingleUse()
    {
        var service = new PairingSessionService(TimeSpan.FromMinutes(5));
        var session = service.Create();

        var first = service.TryConsume(session.Token);
        var second = service.TryConsume(session.Token);

        Assert.NotNull(first.Session);
        Assert.False(first.Expired);
        Assert.Null(second.Session);
        Assert.False(second.Expired);
    }

    [Fact]
    public void ExpiredToken_IsRejected()
    {
        var service = new PairingSessionService(TimeSpan.FromSeconds(-1));
        var session = service.Create();

        var result = service.TryConsume(session.Token);

        Assert.Null(result.Session);
        Assert.True(result.Expired);
    }

    [Fact]
    public void UnknownToken_IsNotFound()
    {
        var service = new PairingSessionService(TimeSpan.FromMinutes(5));

        var result = service.TryConsume("unknown-token");

        Assert.Null(result.Session);
        Assert.False(result.Expired);
    }

    [Fact]
    public void GetActive_ReturnsNull_WhenNone()
    {
        var service = new PairingSessionService(TimeSpan.FromMinutes(5));

        Assert.Null(service.GetActive());
    }
}