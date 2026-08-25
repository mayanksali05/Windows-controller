namespace WinLock.Service.Bluetooth;

/// <summary>
/// Bluetooth proximity state. BLE presence is a proximity signal only and never
/// authorizes operations. The spec's combined "AUTHENTICATED" state is derived
/// client-side (nearby + a valid authenticated session), not reported here.
/// </summary>
public enum ProximityState
{
    Unknown = 0,
    Nearby = 1,
    Away = 2,
}

/// <summary>A snapshot of Bluetooth proximity for the monitored devices.</summary>
public sealed record ProximitySnapshot(
    ProximityState State,
    string? DeviceId,
    int? Rssi,
    DateTimeOffset UpdatedAtUtc)
{
    public static ProximitySnapshot Unknown() =>
        new(ProximityState.Unknown, null, null, DateTimeOffset.UtcNow);
}