using WinLock.Cryptography;
using WinLock.Service.Configuration;
using WinLock.Service.Logging;

namespace WinLock.Service.Bluetooth;

/// <summary>
/// Keeps the BLE scanner in sync with the paired-device store, applies the away
/// timeout, and exposes the current proximity state for status/auth responses.
/// Emits PROXIMITY_CHANGED security events on state transitions.
/// </summary>
public sealed class ProximityMonitor : BackgroundService
{
    private readonly IBluetoothProximityScanner _scanner;
    private readonly AuthorizedDeviceStore _devices;
    private readonly ISecurityEventLogger _log;
    private readonly bool _enabled;
    private ProximityState _lastLogged = ProximityState.Unknown;

    public ProximityMonitor(
        IBluetoothProximityScanner scanner,
        AuthorizedDeviceStore devices,
        ISecurityEventLogger log,
        SecurityOptions security)
    {
        _scanner = scanner;
        _devices = devices;
        _log = log;
        _enabled = security.ProximityEnabled;
        _scanner.SnapshotChanged += (_, snapshot) => LogChange(snapshot);
    }

    public ProximitySnapshot CurrentState => _scanner.Current;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _log.Log(SecurityEventType.ProximityChanged, "BLE proximity monitoring disabled by configuration");
            return;
        }

        _scanner.SetMonitoredDevices(PairedDeviceIds());
        try
        {
            await _scanner.StartAsync(stoppingToken);
            _log.Log(SecurityEventType.ProximityChanged, "BLE proximity monitoring started");
        }
        catch (Exception ex)
        {
            _log.Log(SecurityEventType.ProximityChanged, "BLE proximity scan unavailable",
                new { reason = ex.Message });
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _scanner.SetMonitoredDevices(PairedDeviceIds());
                _scanner.UpdateStale();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.Log(SecurityEventType.ProximityChanged, "BLE proximity scan error",
                    new { reason = ex.Message });
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _scanner.StopAsync(cancellationToken);
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }

    private IReadOnlyCollection<string> PairedDeviceIds() =>
        _devices.GetAll().Select(d => d.DeviceId).ToList();

    private void LogChange(ProximitySnapshot snapshot)
    {
        if (snapshot.State == _lastLogged)
        {
            return;
        }

        _lastLogged = snapshot.State;
        _log.Log(SecurityEventType.ProximityChanged, $"Proximity changed to {snapshot.State}",
            new { deviceId = snapshot.DeviceId, rssi = snapshot.Rssi });
    }
}