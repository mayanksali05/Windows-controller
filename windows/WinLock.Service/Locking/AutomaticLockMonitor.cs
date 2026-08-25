using WinLock.Cryptography;
using WinLock.Service.Bluetooth;
using WinLock.Service.Configuration;
using WinLock.Service.Locking;
using WinLock.Service.Status;

namespace WinLock.Service.Locking;

/// <summary>
/// Optional automatic lock: when the paired phone's BLE proximity leaves
/// NEARBY for a configurable grace period, the workstation is locked. A single
/// dropped scan does not lock — the scanner's away timeout absorbs brief signal
/// loss, and this monitor applies an additional configurable minimum away
/// duration before locking. BLE is proximity-only; automatic lock is a
/// convenience control, never a security boundary.
/// </summary>
public sealed class AutomaticLockMonitor : BackgroundService
{
    private readonly ProximityMonitor _proximity;
    private readonly LockCoordinator _lockCoordinator;
    private readonly IWindowsLockService _lockService;
    private readonly AuthorizedDeviceStore _devices;
    private readonly SecurityOptions _security;
    private readonly ILogger<AutomaticLockMonitor> _logger;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TimeSpan _pollInterval;
    private readonly Func<bool?> _isLockedState;

    private DateTimeOffset? _armedSince;
    private bool _lockIssued;

    public AutomaticLockMonitor(
        ProximityMonitor proximity,
        LockCoordinator lockCoordinator,
        IWindowsLockService lockService,
        AuthorizedDeviceStore devices,
        SecurityOptions security,
        ILogger<AutomaticLockMonitor> logger,
        Func<DateTimeOffset>? utcNow = null,
        TimeSpan? pollInterval = null,
        Func<bool?>? isLockedState = null)
    {
        _proximity = proximity;
        _lockCoordinator = lockCoordinator;
        _lockService = lockService;
        _devices = devices;
        _security = security;
        _logger = logger;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(1);
        _isLockedState = isLockedState ?? SessionLockStateDetector.IsLocked;
    }

    /// <summary>For tests: when the monitor is currently armed for a lock.</summary>
    internal DateTimeOffset? ArmedSince
    {
        get { lock (_sync) { return _armedSince; } }
    }

    private readonly object _sync = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_security.AutomaticLockEnabled)
        {
            _logger.LogInformation("Automatic lock is disabled.");
            return;
        }

        _logger.LogInformation(
            "Automatic lock enabled: locking after {Seconds}s of absence.",
            _security.AutoLockAwayDurationSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var shouldLock = false;
            lock (_sync)
            {
                shouldLock = EvaluateLocked();
            }

            if (shouldLock)
            {
                try
                {
                    await _lockCoordinator.LockAsync(null, stoppingToken);
                    _logger.LogInformation("Automatic lock: workstation locked.");
                }
                catch (LockFailedException)
                {
                    // LockCoordinator already logged the failure.
                }
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Evaluates the proximity state machine. Returns true exactly once when the
    /// away duration has elapsed and a lock should be issued.
    /// </summary>
    internal bool EvaluateLocked()
    {
        var now = _utcNow();

        // Disabled by configuration (defense in depth; ExecuteAsync also guards).
        if (!_security.AutomaticLockEnabled)
        {
            _armedSince = null;
            return false;
        }

        // Without any paired device there is nothing to lock for.
        if (_devices.Count == 0)
        {
            _armedSince = null;
            return false;
        }

        var state = _proximity.CurrentState.State;

        if (state == ProximityState.Nearby)
        {
            _armedSince = null;
            _lockIssued = false;
            return false;
        }

        if (state is ProximityState.Away or ProximityState.Unknown)
        {
            // Lock at most once per absence episode.
            if (_lockIssued)
            {
                return false;
            }

            _armedSince ??= now;
            if (now - _armedSince.Value >= TimeSpan.FromSeconds(_security.AutoLockAwayDurationSeconds))
            {
                _armedSince = null;
                _lockIssued = true;

                if (_isLockedState() == true)
                {
                    return false;
                }

                if (!_lockService.CanLock)
                {
                    _logger.LogWarning(
                        "Automatic lock skipped: process is not in an interactive session.");
                    return false;
                }

                return true;
            }
        }

        return false;
    }
}