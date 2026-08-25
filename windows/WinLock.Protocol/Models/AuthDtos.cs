namespace WinLock.Protocol.Models;

/// <summary>Body of <c>POST /api/v1/auth/challenge</c>.</summary>
public sealed class AuthChallengeRequestDto
{
    public string? DeviceId { get; init; }
}

/// <summary>Response of <c>POST /api/v1/auth/challenge</c>.</summary>
public sealed class AuthChallengeDto
{
    public string ChallengeId { get; init; } = string.Empty;
    /// <summary>The base64url nonce the client must sign.</summary>
    public string Challenge { get; init; } = string.Empty;
    public string ExpiresAt { get; init; } = string.Empty;
}

/// <summary>Body of <c>POST /api/v1/auth/verify</c>.</summary>
public sealed class AuthVerifyRequestDto
{
    public string? ClientDeviceId { get; init; }
    public string? ChallengeId { get; init; }
    /// <summary>ISO8601 timestamp, included verbatim in the signed input.</summary>
    public string? Timestamp { get; init; }
    public string? Signature { get; init; }
}

/// <summary>Response of <c>POST /api/v1/auth/verify</c> on success.</summary>
public sealed class AuthVerifyResponseDto
{
    public string SessionToken { get; init; } = string.Empty;
    public string SessionExpires { get; init; } = string.Empty;
    public string Proximity { get; init; } = "UNKNOWN";
}