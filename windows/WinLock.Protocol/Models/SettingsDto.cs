namespace WinLock.Protocol.Models;

/// <summary>Read-only proximity/auto-lock settings returned by <c>GET /api/v1/settings</c>.</summary>
public sealed class SettingsDto
{
    public bool ProximityEnabled { get; init; }
    public int ProximityAwayTimeoutSeconds { get; init; }
    public int ProximityNearbyRssiThreshold { get; init; }
    public bool AutomaticLockEnabled { get; init; }
    public int AutoLockAwayDurationSeconds { get; init; }
}