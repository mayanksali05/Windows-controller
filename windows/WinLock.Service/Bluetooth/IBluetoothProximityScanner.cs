namespace WinLock.Service.Bluetooth;

/// <summary>
/// Scans BLE advertisements from paired iPhones. The scanner only reports
/// presence and RSSI; it never authorizes anything. Implementations may be
/// hardware-backed (WinRT) or fakes for tests.
/// </summary>
public interface IBluetoothProximityScanner
{
    ProximitySnapshot Current { get; }

    event EventHandler<ProximitySnapshot>? SnapshotChanged;

    /// <summary>Sets the set of paired device ids whose BLE UUIDs are watched.</summary>
    void SetMonitoredDevices(IReadOnlyCollection<string> deviceIds);

    /// <summary>Marks devices that have not been seen within the away timeout.</summary>
    void UpdateStale();

    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}