namespace WinLock.Service.Authentication;

/// <summary>
/// Abstraction over how a request proves identity. Phase 2 supplies a
/// development-only bearer-token implementation; Phase 4 replaces it with the
/// cryptographic challenge-response protocol. Only one implementation is
/// registered in the container at a time.
/// </summary>
public interface IAuthenticationService
{
    Task<AuthenticationResult> AuthenticateAsync(HttpRequest request, CancellationToken cancellationToken);
}