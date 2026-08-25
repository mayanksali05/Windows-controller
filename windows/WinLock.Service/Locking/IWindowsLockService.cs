namespace WinLock.Service.Locking;

/// <summary>Abstraction over locking the Windows workstation.</summary>
public interface IWindowsLockService
{
    /// <summary>True when the current context can invoke LockWorkStation.</summary>
    bool CanLock { get; }

    Task LockAsync(CancellationToken cancellationToken);
}