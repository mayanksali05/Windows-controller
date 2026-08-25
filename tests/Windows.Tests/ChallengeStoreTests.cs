using WinLock.Service.Authentication;
using Xunit;

namespace Windows.Tests;

public sealed class ChallengeStoreTests
{
    [Fact]
    public void Issue_ConsumeOnce_Succeeds()
    {
        var store = new ChallengeStore(TimeSpan.FromMinutes(1));
        var challenge = store.Issue("phone-1");

        var result = store.TryConsume(challenge.ChallengeId, "phone-1");

        Assert.NotNull(result.Challenge);
        Assert.Equal(challenge.Nonce, result.Challenge!.Nonce);
        Assert.False(result.Expired);
        Assert.False(result.DeviceMismatch);
    }

    [Fact]
    public void Replay_IsNotFound()
    {
        var store = new ChallengeStore(TimeSpan.FromMinutes(1));
        var challenge = store.Issue("phone-1");

        store.TryConsume(challenge.ChallengeId, "phone-1");
        var replay = store.TryConsume(challenge.ChallengeId, "phone-1");

        Assert.Null(replay.Challenge);
        Assert.False(replay.Expired);
    }

    [Fact]
    public void Expired_IsRejected()
    {
        var store = new ChallengeStore(TimeSpan.FromSeconds(-1));
        var challenge = store.Issue("phone-1");

        var result = store.TryConsume(challenge.ChallengeId, "phone-1");

        Assert.Null(result.Challenge);
        Assert.True(result.Expired);
    }

    [Fact]
    public void WrongDevice_IsRejected()
    {
        var store = new ChallengeStore(TimeSpan.FromMinutes(1));
        var challenge = store.Issue("phone-1");

        var result = store.TryConsume(challenge.ChallengeId, "phone-2");

        Assert.Null(result.Challenge);
        Assert.True(result.DeviceMismatch);
    }

    [Fact]
    public void NonceIsLongEnough()
    {
        var store = new ChallengeStore(TimeSpan.FromMinutes(1));
        var challenge = store.Issue("phone-1");

        Assert.True(WinLock.Protocol.Base64Url.Decode(challenge.Nonce).Length >= 32);
    }
}