namespace WinLock.Protocol.Models;

/// <summary>Proximity snapshot returned by <c>GET /api/v1/proximity</c>.</summary>
public sealed class ProximityDto
{
    /// <summary>One of UNKNOWN | NEARBY | AWAY (see protocol docs).</summary>
    public string State { get; init; } = "UNKNOWN";

    /// <summary>The paired device id whose advertisement was seen, if any.</summary>
    public string? DeviceId { get; init; }

    /// <summary>Last observed BLE signal strength in dBm, if any.</summary>
    public int? Rssi { get; init; }

    public string UpdatedAt { get; init; } = string.Empty;
}