using System.Runtime.InteropServices;
using System.Text;

namespace WinLock.Service.Locking;

/// <summary>
/// Thin P/Invoke layer for the supported Windows APIs used by the lock and
/// status subsystems. No Windows authentication is bypassed or disabled.
/// </summary>
internal static class NativeMethods
{
    internal const uint DesktopReadObjects = 0x0001;
    internal const int UoiName = 2;

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool GetUserObjectInformation(
        IntPtr hObj, int nIndex, StringBuilder pvInfo, uint nLength, out uint lpnLengthNeeded);

    [DllImport("kernel32.dll")]
    internal static extern uint WTSGetActiveConsoleSessionId();

    /// <summary>
    /// True when the current process runs in the interactive console session.
    /// LockWorkStation only affects the interactive session; a Session 0
    /// Windows Service cannot lock the user's desktop directly.
    /// </summary>
    internal static bool IsInteractiveSession()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return (uint)process.SessionId == WTSGetActiveConsoleSessionId();
    }
}