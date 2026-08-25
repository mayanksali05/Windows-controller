using Microsoft.AspNetCore.Mvc;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Configuration;

namespace WinLock.Service.Api;

[ApiController]
[Route("api/v1")]
[RequireAuthentication]
public sealed class SettingsController : ControllerBase
{
    private readonly SecurityOptions _security;

    public SettingsController(SecurityOptions security) => _security = security;

    /// <summary>
    /// Returns the current proximity/auto-lock settings. Configuration is
    /// changed on the Windows side (tray application or appsettings.json);
    /// this endpoint lets clients display the active policy.
    /// </summary>
    [HttpGet("settings")]
    public ActionResult<ApiResponse<SettingsDto>> Get() =>
        Ok(ApiResponse<SettingsDto>.Ok(new SettingsDto
        {
            ProximityEnabled = _security.ProximityEnabled,
            ProximityAwayTimeoutSeconds = _security.ProximityAwayTimeoutSeconds,
            ProximityNearbyRssiThreshold = _security.ProximityNearbyRssiThreshold,
            AutomaticLockEnabled = _security.AutomaticLockEnabled,
            AutoLockAwayDurationSeconds = _security.AutoLockAwayDurationSeconds,
        }));
}