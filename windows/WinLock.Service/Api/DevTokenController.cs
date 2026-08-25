using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;

namespace WinLock.Service.Api;

/// <summary>
/// DEVELOPMENT-ONLY endpoint that hands out the runtime-generated development
/// bearer token so local clients (iPhone prototype, tray app) can exercise the
/// authenticated API before Phase 4 lands. Returns 404 outside Development.
/// </summary>
[ApiController]
[Route("api/v1")]
public sealed class DevTokenController : ControllerBase
{
    private readonly DevTokenService _tokens;
    private readonly IDevelopmentModeDetector _mode;

    public DevTokenController(DevTokenService tokens, IDevelopmentModeDetector mode)
    {
        _tokens = tokens;
        _mode = mode;
    }

    [AllowAnonymous]
    [HttpGet("dev/token")]
    public ActionResult<ApiResponse<DevTokenDto>> Get()
    {
        if (!_mode.IsDevelopment)
        {
            return NotFound();
        }

        return Ok(ApiResponse<DevTokenDto>.Ok(new DevTokenDto { Token = _tokens.Token }));
    }
}