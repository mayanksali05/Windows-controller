namespace WinLock.Service.Authentication;

/// <summary>
/// A single-use authentication challenge bound to the requesting device.
/// <c>Nonce</c> is the exact base64url string a client must sign.
/// </summary>
public sealed record Challenge(string ChallengeId, string Nonce, string DeviceId, DateTimeOffset ExpiresAtUtc);

/// <summary>Outcome of consuming a challenge.</summary>
public sealed record ChallengeConsumeResult(Challenge? Challenge, bool Expired, bool DeviceMismatch)
{
    public static ChallengeConsumeResult NotFound() => new(null, false, false);
    public static ChallengeConsumeResult TimedOut() => new(null, true, false);
    public static ChallengeConsumeResult WrongDevice() => new(null, false, true);
    public static ChallengeConsumeResult Success(Challenge challenge) => new(challenge, false, false);
}