using System.Collections.Concurrent;
using Windows.Devices.Bluetooth.Advertisement;

namespace WinLock.Service.Bluetooth;

/// <summary>
/// Windows BLE scanner backed by the supported WinRT
/// <see cref="BluetoothLEAdvertisementWatcher"/>. Watches the per-device
/// service UUIDs of paired iPhones and reports presence + RSSI. When Bluetooth
/// is off/unavailable, state is UNKNOWN (the watcher reports Stopped).
/// </summary>
public sealed class WindowsBluetoothProximityScanner : IBluetoothProximityScanner
{
    private readonly object _sync = new();
    private readonly ConcurrentDictionary<string, string> _deviceUuids = new(); // uuid string -> deviceId
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = new();      // deviceId -> last seen (under _sync)
    private readonly TimeSpan _awayTimeout;
    private readonly int _nearbyRssiThreshold;
    private BluetoothLEAdvertisementWatcher? _watcher;
    private ProximitySnapshot _current = ProximitySnapshot.Unknown();

    public WindowsBluetoothProximityScanner(TimeSpan awayTimeout, int nearbyRssiThreshold)
    {
        _awayTimeout = awayTimeout;
        _nearbyRssiThreshold = nearbyRssiThreshold;
    }

    public ProximitySnapshot Current
    {
        get { lock (_sync) { return _current; } }
    }

    public event EventHandler<ProximitySnapshot>? SnapshotChanged;

    public void SetMonitoredDevices(IReadOnlyCollection<string> deviceIds)
    {
        lock (_sync)
        {
            _deviceUuids.Clear();
            foreach (var deviceId in deviceIds)
            {
                _deviceUuids[ProximityUuid.ForDevice(deviceId).ToString().ToLowerInvariant()] = deviceId;
            }

            var unpaired = _lastSeen.Keys.Where(id => !deviceIds.Contains(id)).ToList();
            foreach (var id in unpaired)
            {
                _lastSeen.Remove(id);
            }

            if (_current.DeviceId is not null && !deviceIds.Contains(_current.DeviceId))
            {
                SetSnapshotLocked(ProximitySnapshot.Unknown());
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_watcher is not null)
            {
                return Task.CompletedTask;
            }

            var watcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active,
            };
            watcher.Received += OnAdvertisementReceived;
            watcher.Stopped += OnWatcherStopped;
            watcher.Start();
            _watcher = watcher;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_watcher is null)
            {
                return Task.CompletedTask;
            }

            _watcher.Received -= OnAdvertisementReceived;
            _watcher.Stopped -= OnWatcherStopped;
            _watcher.Stop();
            _watcher = null;
        }

        return Task.CompletedTask;
    }

    public void UpdateStale()
    {
        lock (_sync)
        {
            if (_current.DeviceId is null || _lastSeen.Count == 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var (deviceId, lastSeen) in _lastSeen.ToList())
            {
                if (now - lastSeen > _awayTimeout)
                {
                    _lastSeen.Remove(deviceId);
                    if (deviceId == _current.DeviceId)
                    {
                        SetSnapshotLocked(ProximitySnapshot.Unknown());
                    }
                }
            }
        }
    }

    private void OnAdvertisementReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        string? deviceId = null;
        foreach (var uuid in args.Advertisement.ServiceUuids)
        {
            if (_deviceUuids.TryGetValue(uuid.ToString().ToLowerInvariant(), out var matched))
            {
                deviceId = matched;
                break;
            }
        }

        if (deviceId is null)
        {
            return;
        }

        var rssi = (int)args.RawSignalStrengthInDBm;
        lock (_sync)
        {
            _lastSeen[deviceId] = DateTimeOffset.UtcNow;
            var state = rssi >= _nearbyRssiThreshold ? ProximityState.Nearby : ProximityState.Away;
            SetSnapshotLocked(new ProximitySnapshot(state, deviceId, rssi, DateTimeOffset.UtcNow));
        }
    }

    private void OnWatcherStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        lock (_sync)
        {
            SetSnapshotLocked(ProximitySnapshot.Unknown());
        }
    }

    private void SetSnapshotLocked(ProximitySnapshot snapshot)
    {
        if (_current == snapshot)
        {
            return;
        }

        _current = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }
}