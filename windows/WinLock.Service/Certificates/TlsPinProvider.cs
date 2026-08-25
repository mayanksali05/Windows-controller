using System.Security.Cryptography;
using WinLock.Protocol;
using WinLock.Service.Configuration;

namespace WinLock.Service.Certificates;

/// <summary>
/// Provides the base64url SHA-256 of the HTTPS leaf certificate (DER) so the
/// iPhone can pin TLS before its first connection. The pin travels out of band
/// in the pairing QR payload. Returns null when HTTPS is disabled.
/// </summary>
public sealed class TlsPinProvider
{
    private readonly string? _pin;

    public TlsPinProvider(ServerOptions options)
    {
        if (options.UseHttps)
        {
            var certificate = CertificateProvider.LoadDevelopmentCertificate(options);
            var der = certificate.GetRawCertData();
            _pin = Base64Url.Encode(SHA256.HashData(der));
        }
    }

    public string? TlsPin => _pin;
}