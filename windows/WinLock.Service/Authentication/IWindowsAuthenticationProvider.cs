namespace WinLock.Service.Authentication;

/// <summary>
/// Request to unlock the Windows session on behalf of an authorized iPhone.
/// This type exists to define the extension-point contract only; it is NOT
/// wired to any endpoint, because a fabricated unlock endpoint would be a
/// Windows-authentication bypass. See docs/windows-unlock.md for the research.
/// </summary>
public sealed record UnlockRequest(string DeviceId, string ChallengeId, string Timestamp, string Signature);

/// <summary>Result of an attempted Windows-session unlock.</summary>
public sealed record UnlockResult(bool Success, string? FailureCode = null, string? FailureMessage = null);

/// <summary>
/// Boundary for Windows-session unlock integration.
///
/// A locked Windows session can only be unlocked through the OS credential
/// stack (Winlogon). The supported integration options are:
///
/// 1. A **Windows Credential Provider** (C++ COM, admin-installed) that
///    presents an iPhone-driven tile on the lock screen. Credential providers
///    are NOT enforcement mechanisms: they gather/serialize credentials and the
///    LSA validates them, so the user's real Windows Hello gesture, PIN, or
///    password is still required on the laptop. The iPhone could at most
///    *trigger* that flow, never replace the credential.
/// 2. **FIDO2 / WebAuthn passkeys**: Windows Hello supports security keys /
///    passkeys for local sign-in, and an iPhone could act as a FIDO2
///    authenticator only if it is enrolled as a Windows Hello security key for
///    the account — a deep, user-interactive OS enrollment flow outside the
///    scope of a companion service.
///
/// Neither mechanism is implementable as a safe, supported companion-service
/// feature today, so this provider has NO implementation and unlock is a
/// documented extension point. Any future implementation MUST go through a
/// supported OS mechanism and must never bypass Winlogon, fabricate
/// credentials, inject input, or disable Windows authentication.
/// </summary>
public interface IWindowsAuthenticationProvider
{
    Task<UnlockResult> UnlockAsync(UnlockRequest request, CancellationToken cancellationToken);
}