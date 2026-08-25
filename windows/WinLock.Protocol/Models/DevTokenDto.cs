namespace WinLock.Protocol.Models;

/// <summary>
/// Development-only bearer token returned by <c>GET /api/v1/dev/token</c>.
/// This endpoint exists solely for Phase 2 prototyping and is replaced by the
/// challenge-response authentication protocol (Phase 4). It is only reachable
/// when the service runs in the Development environment.
/// </summary>
public sealed class DevTokenDto
{
    public string Token { get; init; } = string.Empty;
}