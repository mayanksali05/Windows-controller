using System.Collections.Concurrent;
using System.Security.Cryptography;
using WinLock.Protocol;

namespace WinLock.Service.Authentication;

/// <summary>
/// Issues and consumes one-time authentication challenges. Challenges are
/// in-memory and single-use: the challenge is atomically removed on first
/// consumption, so replaying an old challenge/verify exchange is rejected.
/// Nonces and ids come from a CSPRNG.
/// </summary>
public sealed class ChallengeStore
{
    private readonly TimeSpan _lifetime;
    private readonly ConcurrentDictionary<string, Challenge> _challenges = new();

    public ChallengeStore(TimeSpan lifetime)
    {
        _lifetime = lifetime;
    }

    public Challenge Issue(string deviceId)
    {
        var challenge = new Challenge(
            Base64Url.Encode(RandomNumberGenerator.GetBytes(16)),
            Base64Url.Encode(RandomNumberGenerator.GetBytes(32)),
            deviceId,
            DateTimeOffset.UtcNow.Add(_lifetime));

        _challenges[challenge.ChallengeId] = challenge;
        return challenge;
    }

    public ChallengeConsumeResult TryConsume(string challengeId, string deviceId)
    {
        if (!_challenges.TryRemove(challengeId, out var challenge))
        {
            return ChallengeConsumeResult.NotFound();
        }

        if (challenge.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return ChallengeConsumeResult.TimedOut();
        }

        if (!string.Equals(challenge.DeviceId, deviceId, StringComparison.Ordinal))
        {
            return ChallengeConsumeResult.WrongDevice();
        }

        return ChallengeConsumeResult.Success(challenge);
    }

    public void CleanupExpired()
    {
        foreach (var (id, challenge) in _challenges)
        {
            if (challenge.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _challenges.TryRemove(id, out _);
            }
        }
    }
}