using System.Runtime.InteropServices;
using System.Text;

namespace WinLock.Service.Status;

/// <summary>Read-only session-lock state detection via the active input desktop name.</summary>
public static class SessionLockStateDetector
{
    /// <summary>
    /// Returns true when the interactive session is locked (the active input
    /// desktop is "Winlogon"), false when it is not, and null when the state
    /// cannot be determined (e.g. Session 0). Read-only; never manipulates the
    /// login screen.
    /// </summary>
    public static bool? IsLocked()
    {
        var desktop = Locking.NativeMethods.OpenInputDesktop(0, false, Locking.NativeMethods.DesktopReadObjects);
        if (desktop == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var name = new StringBuilder(256);
            if (Locking.NativeMethods.GetUserObjectInformation(
                    desktop, Locking.NativeMethods.UoiName, name, (uint)name.Capacity, out _))
            {
                return string.Equals(name.ToString(), "Winlogon", StringComparison.OrdinalIgnoreCase);
            }

            return null;
        }
        finally
        {
            Locking.NativeMethods.CloseDesktop(desktop);
        }
    }
}

/// <summary>Reads battery status via the supported GetSystemPowerStatus API.</summary>
internal static class SystemPowerStatus
{
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AclLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    /// <summary>Battery percentage 0-100, or null when unknown/desktop.</summary>
    public static int? GetBatteryPercent()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return null;
        }

        return status.BatteryLifePercent == 255 ? null : status.BatteryLifePercent;
    }
}