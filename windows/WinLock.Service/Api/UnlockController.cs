using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Logging;
using WinLock.Service.Unlocking;

namespace WinLock.Service.Api;

/// <summary>
/// Unlocks the locked Windows session with the user's real password, sent by a
/// paired iPhone after Face ID. Requires a valid challenge-response session,
/// and is rate limited. The password is never logged or stored.
/// </summary>
[ApiController]
[Route("api/v1")]
[RequireAuthentication]
public sealed class UnlockController : ControllerBase
{
    public const string RateLimitPolicy = "unlock";

    private readonly IWindowsAuthenticationProvider _unlock;
    private readonly ISecurityEventLogger _log;

    public UnlockController(IWindowsAuthenticationProvider unlock, ISecurityEventLogger log)
    {
        _unlock = unlock;
        _log = log;
    }

    [HttpPost("unlock")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<ActionResult<ApiResponse>> Unlock([FromBody] UnlockRequestDto? body, CancellationToken cancellationToken)
    {
        if (body is null ||
            string.IsNullOrEmpty(body.Password) ||
            body.Password.Length > UnlockPipeProtocol.MaxPasswordChars)
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.MalformedRequest, "Request body is invalid"));
        }

        var deviceId = HttpContext.Items.TryGetValue(AuthenticationActionFilter.AuthenticatedDeviceIdKey, out var value)
            ? value?.ToString()
            : null;

        _log.Log(SecurityEventType.UnlockRequested, "Unlock requested", new { deviceId });

        var result = await _unlock.UnlockAsync(new UnlockRequest(deviceId, body.Password), cancellationToken);

        if (result.Success)
        {
            _log.Log(SecurityEventType.UnlockSuccess, "Workstation unlocked", new { deviceId });
            return Ok(ApiResponse.SuccessResult("Laptop unlocked"));
        }

        _log.Log(SecurityEventType.UnlockFailed, "Unlock failed",
            new { deviceId, outcome = result.Outcome.ToString() });

        return result.Outcome switch
        {
            UnlockOutcome.NotLocked => Conflict(ApiResponse.Failure(ErrorCodes.UnlockNotLocked, result.Message)),
            UnlockOutcome.Unavailable => StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Failure(ErrorCodes.UnlockUnavailable, result.Message)),
            UnlockOutcome.PasswordRejected => UnprocessableEntity(
                ApiResponse.Failure(ErrorCodes.UnlockPasswordRejected, result.Message)),
            UnlockOutcome.Busy => StatusCode(StatusCodes.Status429TooManyRequests,
                ApiResponse.Failure(ErrorCodes.RateLimited, result.Message)),
            _ => StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Failure(ErrorCodes.UnlockFailed, result.Message)),
        };
    }
}
