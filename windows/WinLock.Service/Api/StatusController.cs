using Microsoft.AspNetCore.Mvc;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Status;

namespace WinLock.Service.Api;

[ApiController]
[Route("api/v1")]
[RequireAuthentication]
public sealed class StatusController : ControllerBase
{
    private readonly ISystemStatusService _status;

    public StatusController(ISystemStatusService status) => _status = status;

    [HttpGet("status")]
    public ActionResult<ApiResponse<StatusDto>> GetStatus() =>
        Ok(ApiResponse<StatusDto>.Ok(_status.GetStatus()));
}