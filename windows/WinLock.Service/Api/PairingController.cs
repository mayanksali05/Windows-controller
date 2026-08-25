using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Logging;
using WinLock.Service.Pairing;
using WinLock.Service.Security;

namespace WinLock.Service.Api;

/// <summary>
/// Pairing and device-management endpoints. The one-time pairing token is only
/// ever exposed to the authenticated tray (QR shown on the Windows screen),
/// never to unauthenticated clients, so a LAN attacker cannot pair their own
/// device.
/// </summary>
[ApiController]
[Route("api/v1")]
[RequireAuthentication]
public sealed class PairingController : ControllerBase
{
    private readonly DeviceIdentityService _identity;
    private readonly PairingSessionService _sessions;
    private readonly AuthorizedDeviceStore _devices;
    private readonly ISigningService _signing;
    private readonly ISecurityEventLogger _log;

    public PairingController(
        DeviceIdentityService identity,
        PairingSessionService sessions,
        AuthorizedDeviceStore devices,
        ISigningService signing,
        ISecurityEventLogger log)
    {
        _identity = identity;
        _sessions = sessions;
        _devices = devices;
        _signing = signing;
        _log = log;
    }

    /// <summary>
    /// Anonymous. Returns public identity material so a client can verify the
    /// server holds the Windows private key. Never includes the pairing token.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("pair/request")]
    public ActionResult<ApiResponse<PairingInfoDto>> RequestPairing([FromBody] PairingRequestDto? body)
    {
        if (body?.DeviceId is not { Length: > 0 })
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.MalformedRequest, "Request body is invalid"));
        }

        if (!string.Equals(body.DeviceId, _identity.DeviceId, StringComparison.Ordinal))
        {
            return NotFound(ApiResponse.Failure(ErrorCodes.DeviceUnknown, "Device not found"));
        }

        var session = _sessions.GetActive();
        if (session is null)
        {
            return Ok(ApiResponse<PairingInfoDto>.Ok(new PairingInfoDto
            {
                DeviceId = _identity.DeviceId,
                WindowsPublicKey = _identity.PublicKeyBase64Url,
                PairingAvailable = false,
            }));
        }

        var message = ProtocolStrings.PairingSigningInput(_identity.DeviceId, session.Nonce);
        return Ok(ApiResponse<PairingInfoDto>.Ok(new PairingInfoDto
        {
            DeviceId = _identity.DeviceId,
            WindowsPublicKey = _identity.PublicKeyBase64Url,
            PairingAvailable = true,
            PairingNonce = session.Nonce,
            ExpiresAt = session.ExpiresAtUtc.ToString("O"),
            Signature = Base64Url.Encode(_identity.Sign(message)),
        }));
    }

    /// <summary>
    /// Anonymous. Completes pairing: validates the one-time token, verifies the
    /// client's signature over the pairing nonce (proving key possession), and
    /// stores the client public key. Replayed/expired tokens are rejected.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("pair/confirm")]
    public ActionResult<ApiResponse> Confirm([FromBody] PairingConfirmationDto? body)
    {
        if (body is null ||
            string.IsNullOrWhiteSpace(body.DeviceId) ||
            string.IsNullOrWhiteSpace(body.ClientDeviceId) ||
            string.IsNullOrWhiteSpace(body.ClientPublicKey) ||
            string.IsNullOrWhiteSpace(body.PairingToken) ||
            string.IsNullOrWhiteSpace(body.Signature))
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.MalformedRequest, "Request body is invalid"));
        }

        if (!string.Equals(body.DeviceId, _identity.DeviceId, StringComparison.Ordinal))
        {
            return NotFound(ApiResponse.Failure(ErrorCodes.DeviceUnknown, "Device not found"));
        }

        var consume = _sessions.TryConsume(body.PairingToken);
        if (consume.Session is null)
        {
            _log.Log(SecurityEventType.PairingFailed, "Pairing failed",
                new { reason = consume.Expired ? "expired" : "invalid", client = body.ClientDeviceId });
            var code = consume.Expired ? ErrorCodes.PairingExpired : ErrorCodes.PairingInvalid;
            return BadRequest(ApiResponse.Failure(code,
                consume.Expired ? "Pairing token expired" : "Invalid or already-used pairing token"));
        }

        var publicKey = Base64Url.Decode(body.ClientPublicKey);
        var message = ProtocolStrings.PairingSigningInput(body.ClientDeviceId, consume.Session.Nonce);
        var signature = Base64Url.Decode(body.Signature);

        if (publicKey.Length != Ed25519.PublicKeySize || !_signing.Verify(publicKey, message, signature))
        {
            _log.Log(SecurityEventType.PairingFailed, "Pairing failed",
                new { reason = "signature", client = body.ClientDeviceId });
            return BadRequest(ApiResponse.Failure(ErrorCodes.PairingInvalid, "Signature verification failed"));
        }

        if (!_devices.TryAdd(new AuthorizedDevice
            {
                DeviceId = body.ClientDeviceId,
                PublicKeyBase64Url = body.ClientPublicKey,
                Name = body.ClientDeviceId,
                PairedAtUtc = DateTimeOffset.UtcNow,
            }))
        {
            _log.Log(SecurityEventType.PairingFailed, "Pairing failed",
                new { reason = "already-paired", client = body.ClientDeviceId });
            return Conflict(ApiResponse.Failure(ErrorCodes.DeviceUnauthorized, "Device is already paired"));
        }

        _log.Log(SecurityEventType.PairingCompleted, "Pairing completed",
            new { deviceId = body.ClientDeviceId });
        return Ok(ApiResponse.SuccessResult("Pairing completed"));
    }

    /// <summary>Authenticated. Creates a pairing session and returns the full QR payload.</summary>
    [HttpPost("pair/session")]
    public ActionResult<ApiResponse<PairingSessionPayloadDto>> CreateSession()
    {
        var session = _sessions.Create();
        var message = ProtocolStrings.PairingSigningInput(_identity.DeviceId, session.Nonce);

        var payload = new PairingSessionPayloadDto
        {
            DeviceId = _identity.DeviceId,
            WindowsPublicKey = _identity.PublicKeyBase64Url,
            PairingNonce = session.Nonce,
            PairingToken = session.Token,
            ExpiresAt = session.ExpiresAtUtc.ToString("O"),
            Signature = Base64Url.Encode(_identity.Sign(message)),
        };

        _log.Log(SecurityEventType.PairingStarted, "Pairing session created",
            new { deviceId = _identity.DeviceId });
        return Ok(ApiResponse<PairingSessionPayloadDto>.Ok(payload));
    }

    /// <summary>Authenticated. Lists paired devices.</summary>
    [HttpGet("pair/devices")]
    public ActionResult<ApiResponse<IReadOnlyList<AuthorizedDeviceDto>>> ListDevices()
    {
        var devices = _devices.GetAll()
            .Select(d => new AuthorizedDeviceDto
            {
                DeviceId = d.DeviceId,
                Name = d.Name,
                PairedAt = d.PairedAtUtc.ToString("O"),
            })
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<AuthorizedDeviceDto>>.Ok(devices));
    }

    /// <summary>Authenticated. Removes a paired device.</summary>
    [HttpPost("unpair")]
    public ActionResult<ApiResponse> Unpair([FromBody] UnpairRequestDto? body)
    {
        if (body?.DeviceId is not { Length: > 0 })
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.MalformedRequest, "Request body is invalid"));
        }

        if (!_devices.TryRemove(body.DeviceId))
        {
            return NotFound(ApiResponse.Failure(ErrorCodes.DeviceUnknown, "Device not paired"));
        }

        _log.Log(SecurityEventType.DeviceUnpaired, "Device unpaired", new { deviceId = body.DeviceId });
        return Ok(ApiResponse.SuccessResult("Device unpaired"));
    }
}