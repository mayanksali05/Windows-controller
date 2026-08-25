namespace WinLock.Protocol.Models;

/// <summary>Body of <c>POST /api/v1/pair/request</c> (anonymous).</summary>
public sealed class PairingRequestDto
{
    public string? DeviceId { get; init; }
}

/// <summary>
/// Public pairing material returned by <c>POST /api/v1/pair/request</c>.
/// Contains no secret: the one-time pairing token is only ever shown on the
/// Windows screen as a QR code. The signature proves the server holds the
/// Windows private key (identity verification).
/// </summary>
public sealed class PairingInfoDto
{
    public string DeviceId { get; init; } = string.Empty;
    public string WindowsPublicKey { get; init; } = string.Empty;
    public bool PairingAvailable { get; init; }
    public string? PairingNonce { get; init; }
    public string? ExpiresAt { get; init; }
    public string? Signature { get; init; }
}

/// <summary>
/// Full payload displayed as a QR code (and returned to the authenticated tray
/// via <c>POST /api/v1/pair/session</c>). Includes the one-time pairing token,
/// so this must never be returned to unauthenticated clients.
/// </summary>
public sealed class PairingSessionPayloadDto
{
    public int Version { get; init; } = 1;
    public string DeviceId { get; init; } = string.Empty;
    public string WindowsPublicKey { get; init; } = string.Empty;
    public string PairingNonce { get; init; } = string.Empty;
    public string PairingToken { get; init; } = string.Empty;
    public string ExpiresAt { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;

    /// <summary>
    /// Base64url SHA-256 of the HTTPS leaf certificate (DER), delivered out of
    /// band via the QR so the iPhone can pin TLS before its first connection.
    /// Empty when HTTPS is disabled (development only).
    /// </summary>
    public string TlsPin { get; init; } = string.Empty;
}

/// <summary>Body of <c>POST /api/v1/pair/confirm</c> (anonymous).</summary>
public sealed class PairingConfirmationDto
{
    public string? DeviceId { get; init; }
    public string? ClientDeviceId { get; init; }
    public string? ClientPublicKey { get; init; }
    public string? PairingToken { get; init; }
    public string? Signature { get; init; }
}

/// <summary>Body of <c>POST /api/v1/unpair</c> (authenticated).</summary>
public sealed class UnpairRequestDto
{
    public string? DeviceId { get; init; }
}

/// <summary>An authorized (paired) iPhone as returned by <c>GET /api/v1/pair/devices</c>.</summary>
public sealed class AuthorizedDeviceDto
{
    public string DeviceId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string PairedAt { get; init; } = string.Empty;
}