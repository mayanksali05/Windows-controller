namespace WinLock.Protocol;

/// <summary>
/// Stable error codes returned to clients in the structured error envelope.
/// These are part of the public protocol and must not change without a
/// protocol version bump.
/// </summary>
public static class ErrorCodes
{
    public const string AuthFailed = "AUTH_FAILED";
    public const string ChallengeExpired = "CHALLENGE_EXPIRED";
    public const string ChallengeReplayed = "CHALLENGE_REPLAYED";
    public const string DeviceUnknown = "DEVICE_UNKNOWN";
    public const string DeviceUnauthorized = "DEVICE_UNAUTHORIZED";
    public const string PairingInvalid = "PAIRING_INVALID";
    public const string PairingExpired = "PAIRING_EXPIRED";
    public const string LockFailed = "LOCK_FAILED";
    public const string RateLimited = "RATE_LIMITED";
    public const string MalformedRequest = "MALFORMED_REQUEST";
    public const string InternalError = "INTERNAL_ERROR";
}