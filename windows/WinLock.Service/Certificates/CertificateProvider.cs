using System.Security.Cryptography.X509Certificates;
using WinLock.Service.Configuration;

namespace WinLock.Service.Certificates;

/// <summary>
/// Loads the HTTPS certificate for the service. In development this is the
/// CN=WinLock-Development certificate created by scripts/setup-windows.ps1.
/// The service never starts without a certificate (fail-secure).
/// </summary>
public static class CertificateProvider
{
    public static X509Certificate2 LoadDevelopmentCertificate(ServerOptions options)
    {
        var preferred = Find(cert =>
            cert.HasPrivateKey &&
            cert.NotAfter > DateTime.UtcNow &&
            (string.IsNullOrWhiteSpace(options.CertificateThumbprint)
                ? cert.Subject.Contains("CN=WinLock-Development", StringComparison.OrdinalIgnoreCase)
                : cert.Thumbprint.Equals(options.CertificateThumbprint, StringComparison.OrdinalIgnoreCase)));

        if (preferred is not null)
        {
            return preferred;
        }

        throw new InvalidOperationException(
            "HTTPS certificate not found. Run scripts/setup-windows.ps1 to create the " +
            "CN=WinLock-Development certificate, or set Server:CertificateThumbprint. " +
            "Refusing to start without TLS.");
    }

    private static X509Certificate2? Find(Func<X509Certificate2, bool> predicate)
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        foreach (var cert in store.Certificates)
        {
            if (predicate(cert))
            {
                return new X509Certificate2(cert);
            }
        }
        return null;
    }
}