namespace WinLock.Protocol.Models;

/// <summary>
/// Snapshot of laptop status returned by <c>GET /api/v1/status</c>.
/// <c>Proximity</c> and <c>Security</c> report honest initial values until the
/// BLE (Phase 6) and pairing (Phase 3) subsystems are implemented.
/// </summary>
public sealed class StatusDto
{
    /// <summary>True when the interactive session is locked; null when undeterminable.</summary>
    public bool? IsLocked { get; init; }

    /// <summary>Battery percentage 0-100; null when unavailable (e.g. desktop).</summary>
    public int? BatteryPercent { get; init; }

    public string ServiceVersion { get; init; } = string.Empty;
    public string Environment { get; init; } = string.Empty;

    /// <summary>True when the running context can call LockWorkStation.</summary>
    public bool LockAvailable { get; init; }

    /// <summary>One of UNKNOWN | NEARBY | AWAY | AUTHENTICATED (see protocol docs).</summary>
    public string Proximity { get; init; } = "UNKNOWN";

    /// <summary>One of NOT_PAIRED | PAIRED (see protocol docs).</summary>
    public string Security { get; init; } = "NOT_PAIRED";
}