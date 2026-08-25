using WinLock.Protocol;

namespace WinLock.Service.Authentication;

/// <summary>
/// Development-only bearer-token authentication. Isolated from production
/// security: the service refuses to start outside the Development environment
/// while this is the registered provider (see Program).
/// </summary>
public sealed class DevelopmentAuthenticationService : IAuthenticationService
{
    private const string BearerPrefix = "Bearer ";
    private readonly DevTokenService _tokens;

    public DevelopmentAuthenticationService(DevTokenService tokens) => _tokens = tokens;

    public Task<AuthenticationResult> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticationResult.Fail(ErrorCodes.AuthFailed, "Missing bearer token"));
        }

        var token = header[BearerPrefix.Length..].Trim();
        return Task.FromResult(_tokens.Validate(token)
            ? AuthenticationResult.Ok()
            : AuthenticationResult.Fail(ErrorCodes.AuthFailed, "Invalid token"));
    }
}