using System.Security.Cryptography;
using System.Text.Json;
using WinLock.Protocol;

namespace WinLock.Service.Authentication;

/// <summary>
/// Issues and validates HMAC-SHA256 signed session tokens
/// <c>&lt;base64url(payload)&gt;.&lt;base64url(hmac)&gt;</c>. The signing key is
/// generated at startup and kept in memory, so all sessions end on restart
/// (fail-safe). Tokens carry the device id and an absolute expiry.
/// </summary>
public sealed class SessionTokenService
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly TimeSpan _lifetime;

    public SessionTokenService(TimeSpan lifetime)
    {
        _lifetime = lifetime;
    }

    public SessionInfo Issue(string deviceId)
    {
        var expires = DateTimeOffset.UtcNow.Add(_lifetime);
        var payload = new SessionPayload
        {
            Dev = deviceId,
            Exp = expires.ToUnixTimeSeconds(),
            Jti = Base64Url.Encode(RandomNumberGenerator.GetBytes(16)),
        };

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = HMACSHA256.HashData(_key, payloadBytes);
        var token = $"{Base64Url.Encode(payloadBytes)}.{Base64Url.Encode(signature)}";

        return new SessionInfo(token, deviceId, expires);
    }

    public SessionInfo? Validate(string token)
    {
        var separator = token.LastIndexOf('.');
        if (separator <= 0)
        {
            return null;
        }

        byte[] payloadBytes;
        byte[] signature;
        try
        {
            payloadBytes = Base64Url.Decode(token[..separator]);
            signature = Base64Url.Decode(token[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        var expected = HMACSHA256.HashData(_key, payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(signature, expected))
        {
            return null;
        }

        SessionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionPayload>(payloadBytes);
        }
        catch (JsonException)
        {
            return null;
        }

        if (payload is null || string.IsNullOrEmpty(payload.Dev))
        {
            return null;
        }

        var expires = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
        if (expires <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return new SessionInfo(token, payload.Dev, expires);
    }

    private sealed class SessionPayload
    {
        public string Dev { get; set; } = string.Empty;
        public long Exp { get; set; }
        public string Jti { get; set; } = string.Empty;
    }
}