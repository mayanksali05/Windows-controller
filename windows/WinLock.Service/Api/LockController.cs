using Microsoft.AspNetCore.Mvc;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Locking;

namespace WinLock.Service.Api;

[ApiController]
[Route("api/v1")]
[RequireAuthentication]
public sealed class LockController : ControllerBase
{
    private readonly LockCoordinator _coordinator;

    public LockController(LockCoordinator coordinator) => _coordinator = coordinator;

    [HttpPost("lock")]
    public async Task<ActionResult<ApiResponse>> Lock([FromBody] LockRequest? request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.MalformedRequest, "Request body is invalid"));
        }

        try
        {
            await _coordinator.LockAsync(request.DeviceId, cancellationToken);
            return Ok(ApiResponse.SuccessResult("Laptop locked successfully"));
        }
        catch (LockFailedException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Failure(ErrorCodes.LockFailed, "Could not lock the workstation"));
        }
    }
}