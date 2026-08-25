using Microsoft.AspNetCore.Mvc;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Bluetooth;

namespace WinLock.Service.Api;

[ApiController]
[Route("api/v1")]
[RequireAuthentication]
public sealed class ProximityController : ControllerBase
{
    private readonly ProximityMonitor _monitor;

    public ProximityController(ProximityMonitor monitor) => _monitor = monitor;

    [HttpGet("proximity")]
    public ActionResult<ApiResponse<ProximityDto>> Get()
    {
        var snapshot = _monitor.CurrentState;
        return Ok(ApiResponse<ProximityDto>.Ok(new ProximityDto
        {
            State = snapshot.State.ToString().ToUpperInvariant(),
            DeviceId = snapshot.DeviceId,
            Rssi = snapshot.Rssi,
            UpdatedAt = snapshot.UpdatedAtUtc.ToString("O"),
        }));
    }
}