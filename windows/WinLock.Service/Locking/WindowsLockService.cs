using System.Runtime.InteropServices;

namespace WinLock.Service.Locking;

/// <summary>
/// Locks the workstation via the supported <c>LockWorkStation</c> API. Requires
/// an interactive session; throws <see cref="LockFailedException"/> otherwise.
/// </summary>
public sealed class WindowsLockService : IWindowsLockService
{
    public bool CanLock => NativeMethods.IsInteractiveSession();

    public Task LockAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!CanLock)
        {
            throw new LockFailedException(
                "LockWorkStation requires an interactive session. The process is running " +
                "in a non-interactive session (Session 0).");
        }

        if (!NativeMethods.LockWorkStation())
        {
            var error = Marshal.GetLastWin32Error();
            throw new LockFailedException($"LockWorkStation failed (Win32 error {error}).");
        }

        return Task.CompletedTask;
    }
}