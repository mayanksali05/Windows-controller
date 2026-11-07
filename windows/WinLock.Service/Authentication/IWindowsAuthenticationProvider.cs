namespace WinLock.Service.Authentication;

/// <summary>
/// Request to unlock the locked Windows session with the user's real password.
/// Not a record, so the compiler does not generate a ToString that prints it.
/// </summary>
public sealed class UnlockRequest
{
    public UnlockRequest(string? deviceId, string password)
    {
        DeviceId = deviceId;
        Password = password;
    }

    public string? DeviceId { get; }

    public string Password { get; }

    public override string ToString() => $"UnlockRequest {{ DeviceId = {DeviceId}, Password = *** }}";
}

public enum UnlockOutcome
{
    Unlocked,
    NotLocked,
    Unavailable,
    PasswordRejected,
    Busy,
    TimedOut,
    Failed,
}

/// <summary>Result of an attempted Windows-session unlock (never contains the password).</summary>
public sealed record UnlockResult(UnlockOutcome Outcome, string Message)
{
    public bool Success => Outcome == UnlockOutcome.Unlocked;
}

/// <summary>
/// Boundary for Windows-session unlock.
///
/// A locked session can only be unlocked through the OS credential stack
/// (Winlogon/LogonUI). The implementation (<c>CredentialProviderUnlockService</c>)
/// hands the user's real password to the WinLock credential provider
/// (<c>windows/WinLock.CredentialProvider</c>), which submits it to Windows as a
/// standard unlock logon. Windows validates it exactly as if it were typed:
/// nothing is bypassed, and a wrong password is rejected like a wrong password.
/// </summary>
public interface IWindowsAuthenticationProvider
{
    /// <summary>True when the credential provider is installed and enabled.</summary>
    bool IsAvailable { get; }

    Task<UnlockResult> UnlockAsync(UnlockRequest request, CancellationToken cancellationToken);
}
