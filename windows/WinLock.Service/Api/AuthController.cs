using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WinLock.Cryptography;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service.Authentication;
using WinLock.Service.Bluetooth;
using WinLock.Service.Configuration;
using WinLock.Service.Logging;
using WinLock.Service.Security;

namespace WinLock.Service.Api;

/// <summary>
/// Challenge-response authentication endpoints. A paired client requests a
/// one-time challenge, signs it (device_id | nonce | timestamp | endpoint)
/// with its private key, and receives a short-lived session token. Replays,
/// expired challenges, stale timestamps, and unknown devices are rejected.
/// </summary>
[ApiController]
[Route("api/v1")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly ChallengeStore _challenges;
    private readonly SessionTokenService _sessions;
    private readonly DeviceAuthorizer _devices;
    private readonly SecurityOptions _security;
    private readonly ISecurityEventLogger _log;
    private readonly ProximityMonitor _proximity;

    public AuthController(
        ChallengeStore challenges,
        SessionTokenService sessions,
        DeviceAuthorizer devices,
        SecurityOptions security,
        ISecurityEventLogger log,
        ProximityMonitor proximity)
    {
        _challenges = challenges;
        _sessions = sessions;
        _devices = devices;
        _security = security;
        _log = log;
        _proximity = proximity;
    }

    [HttpPost("auth/challenge")]
    public ActionResult<ApiResponse<AuthChallengeDto>> Challenge([FromBody] AuthChallengeRequestDto? body)
    {
        if (body?.DeviceId is not { Length: > 0 })
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.MalformedRequest, "Request body is invalid"));
        }

        if (!_devices.IsAuthorized(body.DeviceId))
        {
            _log.Log(SecurityEventType.AuthenticationFailed, "Challenge requested for unknown device",
                new { deviceId = body.DeviceId });
            return NotFound(ApiResponse.Failure(ErrorCodes.DeviceUnknown, "Device is not paired"));
        }

        var challenge = _challenges.Issue(body.DeviceId);
        _log.Log(SecurityEventType.AuthenticationStarted, "Challenge issued", new { deviceId = body.DeviceId });

        return Ok(ApiResponse<AuthChallengeDto>.Ok(new AuthChallengeDto
        {
            ChallengeId = challenge.ChallengeId,
            Challenge = challenge.Nonce,
            ExpiresAt = challenge.ExpiresAtUtc.ToString("O"),
        }));
    }

    [HttpPost("auth/verify")]
    public ActionResult<ApiResponse<AuthVerifyResponseDto>> Verify([FromBody] AuthVerifyRequestDto? body)
    {
        if (body is null ||
            string.IsNullOrWhiteSpace(body.ClientDeviceId) ||
            string.IsNullOrWhiteSpace(body.ChallengeId) ||
            string.IsNullOrWhiteSpace(body.Timestamp) ||
            string.IsNullOrWhiteSpace(body.Signature))
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.MalformedRequest, "Request body is invalid"));
        }

        var consume = _challenges.TryConsume(body.ChallengeId, body.ClientDeviceId);
        if (consume.Challenge is null)
        {
            _log.Log(SecurityEventType.AuthenticationFailed, "Verification failed", new
            {
                reason = consume.Expired ? "expired" : consume.DeviceMismatch ? "device-mismatch" : "unknown-challenge",
                deviceId = body.ClientDeviceId,
            });

            var code = consume.Expired ? ErrorCodes.ChallengeExpired : ErrorCodes.ChallengeReplayed;
            return BadRequest(ApiResponse.Failure(code,
                consume.Expired ? "Challenge expired" : "Challenge is invalid or already used"));
        }

        if (!DateTimeOffset.TryParse(body.Timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var timestamp))
        {
            return BadRequest(ApiResponse.Failure(ErrorCodes.AuthFailed, "Timestamp is invalid"));
        }

        var skew = TimeSpan.FromSeconds(_security.MaxClockSkewSeconds);
        if (Math.Abs((DateTimeOffset.UtcNow - timestamp).TotalSeconds) > skew.TotalSeconds)
        {
            _log.Log(SecurityEventType.AuthenticationFailed, "Verification failed",
                new { reason = "clock-skew", deviceId = body.ClientDeviceId });
            return BadRequest(ApiResponse.Failure(ErrorCodes.AuthFailed, "Timestamp is out of range"));
        }

        var publicKey = _devices.GetPublicKey(body.ClientDeviceId);
        if (publicKey is null)
        {
            return NotFound(ApiResponse.Failure(ErrorCodes.DeviceUnknown, "Device is not paired"));
        }

        var input = ProtocolStrings.AuthenticationSigningInput(
            body.ClientDeviceId, consume.Challenge.Nonce, body.Timestamp, ProtocolStrings.ChallengeVerifyEndpoint);

        if (!Ed25519.Verify(publicKey, input, Base64Url.Decode(body.Signature)))
        {
            _log.Log(SecurityEventType.AuthenticationFailed, "Verification failed",
                new { reason = "signature", deviceId = body.ClientDeviceId });
            return BadRequest(ApiResponse.Failure(ErrorCodes.AuthFailed, "Signature verification failed"));
        }

        var session = _sessions.Issue(body.ClientDeviceId);
        _log.Log(SecurityEventType.AuthenticationSuccess, "Authentication succeeded",
            new { deviceId = body.ClientDeviceId });

        return Ok(ApiResponse<AuthVerifyResponseDto>.Ok(new AuthVerifyResponseDto
        {
            SessionToken = session.Token,
            SessionExpires = session.ExpiresAtUtc.ToString("O"),
            Proximity = _proximity.CurrentState.State.ToString().ToUpperInvariant(),
        }));
    }
}