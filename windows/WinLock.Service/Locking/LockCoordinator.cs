using WinLock.Service.Logging;

namespace WinLock.Service.Locking;

/// <summary>
/// Orchestrates a lock request: logs the event, invokes the OS lock, and logs
/// the outcome. Controllers depend on this so locking behavior is testable
/// without touching the real workstation.
/// </summary>
public sealed class LockCoordinator
{
    private readonly IWindowsLockService _lockService;
    private readonly ISecurityEventLogger _log;

    public LockCoordinator(IWindowsLockService lockService, ISecurityEventLogger log)
    {
        _lockService = lockService;
        _log = log;
    }

    public async Task LockAsync(string? deviceId, CancellationToken cancellationToken)
    {
        _log.Log(SecurityEventType.LockRequested, "Lock requested", new { deviceId });

        try
        {
            await _lockService.LockAsync(cancellationToken);
            _log.Log(SecurityEventType.LockSuccess, "Workstation locked");
        }
        catch (Exception ex) when (ex is LockFailedException or OperationCanceledException)
        {
            _log.Log(SecurityEventType.LockFailed, "Lock failed", new { reason = ex.Message });
            throw;
        }
    }
}