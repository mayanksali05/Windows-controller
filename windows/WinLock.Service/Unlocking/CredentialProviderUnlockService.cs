using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;
using WinLock.Service.Authentication;
using WinLock.Service.Status;

namespace WinLock.Service.Unlocking;

/// <summary>
/// Unlocks the locked session by handing the user's password to the WinLock
/// credential provider running inside LogonUI, over a local named pipe.
///
/// Safety properties:
/// <list type="bullet">
/// <item>The password is only written to a pipe whose owner is SYSTEM (the
///   provider creates it with <c>O:SY</c>); an ordinary process that squats on
///   the pipe name cannot receive it.</item>
/// <item>The provider only accepts requests from the locked user's own
///   processes and submits the password to Windows as a normal unlock logon.</item>
/// <item>The password is never logged or stored; the request buffer is zeroed.</item>
/// </list>
/// </summary>
public sealed class CredentialProviderUnlockService : IWindowsAuthenticationProvider
{
    public const string ProviderClsid = "{B8C6F5E2-4A5D-4C1B-9E0F-3D2A7C8B9E41}";

    private const string ProviderRegistryKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\" + ProviderClsid;

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromSeconds(20);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Func<bool?> _isLocked;
    private readonly ILogger<CredentialProviderUnlockService> _logger;

    public CredentialProviderUnlockService(ILogger<CredentialProviderUnlockService> logger)
        : this(logger, SessionLockStateDetector.IsLocked)
    {
    }

    internal CredentialProviderUnlockService(ILogger<CredentialProviderUnlockService> logger, Func<bool?> isLocked)
    {
        _logger = logger;
        _isLocked = isLocked;
    }

    public bool IsAvailable => OperatingSystem.IsWindows() && IsProviderRegistered();

    public async Task<UnlockResult> UnlockAsync(UnlockRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!OperatingSystem.IsWindows())
        {
            return new UnlockResult(UnlockOutcome.Unavailable, "Unlock is only supported on Windows.");
        }

        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return new UnlockResult(UnlockOutcome.Busy, "An unlock is already in progress.");
        }

        try
        {
            return await UnlockCoreAsync(request, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    [SupportedOSPlatform("windows")]
    private async Task<UnlockResult> UnlockCoreAsync(UnlockRequest request, CancellationToken cancellationToken)
    {
        if (_isLocked() == false)
        {
            return new UnlockResult(UnlockOutcome.NotLocked, "The laptop is not locked.");
        }

        if (!IsProviderRegistered())
        {
            return new UnlockResult(UnlockOutcome.Unavailable,
                "The WinLock credential provider is not installed on this laptop " +
                "(run scripts\\install-credential-provider.ps1 as administrator).");
        }

        using var pipe = new NamedPipeClientStream(
            ".", UnlockPipeProtocol.PipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous, TokenImpersonationLevel.Identification);

        try
        {
            await pipe.ConnectAsync(ConnectTimeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            return new UnlockResult(UnlockOutcome.Unavailable,
                "The lock screen is not ready for WinLock. Make sure the laptop is locked and the credential provider is installed.");
        }
        catch (IOException ex)
        {
            _logger.LogWarning("Unlock pipe connect failed: {Message}", ex.Message);
            return new UnlockResult(UnlockOutcome.Unavailable, "The lock screen is not ready for WinLock.");
        }

        if (!IsOwnedBySystem(pipe))
        {
            _logger.LogWarning("Refusing to send credentials: the unlock pipe is not owned by SYSTEM.");
            return new UnlockResult(UnlockOutcome.Failed,
                "Refused to unlock: the lock-screen endpoint could not be verified.");
        }

        pipe.ReadMode = PipeTransmissionMode.Message;

        byte[] message;
        try
        {
            message = UnlockPipeProtocol.EncodeRequest(request.Password);
        }
        catch (ArgumentException)
        {
            return new UnlockResult(UnlockOutcome.Failed, "The password has an unsupported length or characters.");
        }

        try
        {
            await pipe.WriteAsync(message, cancellationToken);
            await pipe.FlushAsync(cancellationToken);
        }
        catch (IOException ex)
        {
            _logger.LogWarning("Unlock pipe write failed: {Message}", ex.Message);
            return new UnlockResult(UnlockOutcome.Failed, "Could not reach the lock screen.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
        }

        var response = new byte[16];
        int read;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            timeout.CancelAfter(ResultTimeout);
            try
            {
                read = await pipe.ReadAsync(response, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new UnlockResult(UnlockOutcome.TimedOut, "The lock screen did not respond in time.");
            }
            catch (IOException)
            {
                // LogonUI can exit as soon as the session unlocks, breaking the pipe.
                read = 0;
            }
        }

        if (read == 0)
        {
            return await ConfirmUnlockedAsync(cancellationToken)
                ? new UnlockResult(UnlockOutcome.Unlocked, "Laptop unlocked")
                : new UnlockResult(UnlockOutcome.Failed, "The lock screen closed the connection without unlocking.");
        }

        if (!UnlockPipeProtocol.TryDecodeResponse(response.AsSpan(0, read), out var code, out var ntStatus))
        {
            return new UnlockResult(UnlockOutcome.Failed, "The lock screen returned an invalid response.");
        }

        switch (code)
        {
            case ProviderResultCode.Success:
                await ConfirmUnlockedAsync(cancellationToken);
                return new UnlockResult(UnlockOutcome.Unlocked, "Laptop unlocked");
            case ProviderResultCode.Rejected:
                _logger.LogInformation("Windows rejected the unlock credential (NTSTATUS 0x{Status:X8}).", ntStatus);
                return new UnlockResult(UnlockOutcome.PasswordRejected, DescribeRejection(ntStatus));
            case ProviderResultCode.NoUser:
                return new UnlockResult(UnlockOutcome.Failed,
                    "The laptop is locked by a different Windows user than the one running WinLock.");
            case ProviderResultCode.Timeout:
                return new UnlockResult(UnlockOutcome.TimedOut,
                    "Windows did not pick up the unlock request. Wake the laptop screen and try again.");
            case ProviderResultCode.Busy:
                return new UnlockResult(UnlockOutcome.Busy, "An unlock is already in progress.");
            default:
                return new UnlockResult(UnlockOutcome.Failed, "The lock screen could not process the request.");
        }
    }

    /// <summary>Gives Windows a moment to switch desktops, then checks the lock state.</summary>
    private async Task<bool> ConfirmUnlockedAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 12; attempt++)
        {
            if (_isLocked() == false)
            {
                return true;
            }
            await Task.Delay(250, cancellationToken);
        }
        return _isLocked() == false;
    }

    private static string DescribeRejection(int ntStatus) => unchecked((uint)ntStatus) switch
    {
        0xC000006D or 0xC000006A => "Windows rejected the password. Update the unlock password in the WinLock app.",
        0xC0000234 => "The Windows account is locked out after too many failed attempts.",
        0xC0000072 => "The Windows account is disabled.",
        0xC0000071 or 0xC0000224 => "The Windows password has expired or must be changed on the laptop.",
        _ => "Windows rejected the unlock.",
    };

    [SupportedOSPlatform("windows")]
    private static bool IsProviderRegistered()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ProviderRegistryKey);
            if (key is null)
            {
                return false;
            }
            return key.GetValue("Disabled") is not int disabled || disabled == 0;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private bool IsOwnedBySystem(NamedPipeClientStream pipe)
    {
        try
        {
            var security = pipe.GetAccessControl();
            var owner = security.GetOwner(typeof(SecurityIdentifier)) as SecurityIdentifier;
            return owner is not null && owner.IsWellKnown(WellKnownSidType.LocalSystemSid);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or InvalidOperationException or PrivilegeNotHeldException)
        {
            _logger.LogWarning("Could not read the unlock pipe owner: {Message}", ex.Message);
            return false;
        }
    }
}
