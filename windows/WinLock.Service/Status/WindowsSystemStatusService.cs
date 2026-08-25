using WinLock.Cryptography;
using WinLock.Protocol.Models;
using WinLock.Service.Bluetooth;
using WinLock.Service.Configuration;
using WinLock.Service.Locking;

namespace WinLock.Service.Status;

/// <summary>Produces the laptop status snapshot returned by <c>GET /api/v1/status</c>.</summary>
public interface ISystemStatusService
{
    StatusDto GetStatus();
}

public sealed class WindowsSystemStatusService : ISystemStatusService
{
    private readonly IWindowsLockService _lockService;
    private readonly AuthorizedDeviceStore _devices;
    private readonly ProximityMonitor _proximity;
    private readonly string _version;
    private readonly string _environment;

    public WindowsSystemStatusService(
        IWindowsLockService lockService,
        AuthorizedDeviceStore devices,
        ProximityMonitor proximity,
        ServerOptions serverOptions)
    {
        _lockService = lockService;
        _devices = devices;
        _proximity = proximity;
        _version = typeof(WindowsSystemStatusService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        _environment = string.Equals(serverOptions.Environment, "Development", StringComparison.OrdinalIgnoreCase)
            ? "Development"
            : "Production";
    }

    public StatusDto GetStatus() => new()
    {
        IsLocked = SessionLockStateDetector.IsLocked(),
        BatteryPercent = SystemPowerStatus.GetBatteryPercent(),
        ServiceVersion = _version,
        Environment = _environment,
        LockAvailable = _lockService.CanLock,
        Proximity = _proximity.CurrentState.State.ToString().ToUpperInvariant(),
        Security = _devices.Count > 0 ? "PAIRED" : "NOT_PAIRED",
    };
}