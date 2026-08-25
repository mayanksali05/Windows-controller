namespace WinLock.Service.Pairing;

/// <summary>A single-use pairing session.</summary>
public sealed class PairingSession
{
    public string Token { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; init; }

    public bool IsExpired => ExpiresAtUtc <= DateTimeOffset.UtcNow;
}

/// <summary>Outcome of consuming a pairing token.</summary>
public sealed record PairingConsumeResult(PairingSession? Session, bool Expired)
{
    public static PairingConsumeResult NotFound() => new(null, false);
    public static PairingConsumeResult TimedOut() => new(null, true);
    public static PairingConsumeResult Success(PairingSession session) => new(session, false);
}