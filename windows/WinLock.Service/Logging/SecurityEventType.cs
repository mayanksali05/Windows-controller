namespace WinLock.Service.Logging;

/// <summary>Structured security event types (see protocol/docs for semantics).</summary>
public enum SecurityEventType
{
    PairingStarted,
    PairingCompleted,
    PairingFailed,
    AuthenticationStarted,
    AuthenticationSuccess,
    AuthenticationFailed,
    LockRequested,
    LockSuccess,
    LockFailed,
    UnlockRequested,
    UnlockSuccess,
    UnlockFailed,
    ProximityChanged,
    DeviceUnpaired,
}