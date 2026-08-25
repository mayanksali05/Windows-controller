using WinLock.Protocol;
using WinLock.Service.Security;

namespace WinLock.Service.Authentication;

/// <summary>
/// Production authentication: validates the short-lived HMAC session token
/// produced by a successful <c>POST /api/v1/auth/verify</c>. Also re-checks
/// that the device is still authorized (so unpairing revokes immediately).
/// </summary>
public sealed class ChallengeResponseAuthenticationService : IAuthenticationService
{
    private const string BearerPrefix = "Bearer ";

    private readonly SessionTokenService _sessions;
    private readonly DeviceAuthorizer _devices;

    public ChallengeResponseAuthenticationService(SessionTokenService sessions, DeviceAuthorizer devices)
    {
        _sessions = sessions;
        _devices = devices;
    }

    public Task<AuthenticationResult> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticationResult.Fail(ErrorCodes.AuthFailed, "Missing bearer token"));
        }

        var token = header[BearerPrefix.Length..].Trim();
        var session = _sessions.Validate(token);
        if (session is null)
        {
            return Task.FromResult(AuthenticationResult.Fail(ErrorCodes.AuthFailed, "Invalid or expired session token"));
        }

        if (!_devices.IsAuthorized(session.DeviceId))
        {
            return Task.FromResult(AuthenticationResult.Fail(ErrorCodes.AuthFailed, "Device is no longer authorized"));
        }

        return Task.FromResult(AuthenticationResult.Ok(session.DeviceId));
    }
}