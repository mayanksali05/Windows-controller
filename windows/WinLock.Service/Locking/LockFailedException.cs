namespace WinLock.Service.Locking;

/// <summary>Raised when the OS refused to lock the workstation.</summary>
public sealed class LockFailedException : Exception
{
    public LockFailedException(string message) : base(message)
    {
    }
}