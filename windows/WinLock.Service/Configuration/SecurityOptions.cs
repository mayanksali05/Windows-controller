namespace WinLock.Service.Configuration;

/// <summary>Authentication/security tuning (<c>Security</c> section). Used by the
/// challenge-response protocol in Phase 4.</summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    public int ChallengeLifetimeSeconds { get; set; } = 30;
    public int MaxClockSkewSeconds { get; set; } = 60;
    public int SessionLifetimeMinutes { get; set; } = 10;
    public int PairingTokenLifetimeSeconds { get; set; } = 300;
}