using System.Security.Cryptography.X509Certificates;

namespace WinLock.Tray;

/// <summary>
/// Loads the development certificate by pinned identity (subject name) so the
/// tray can validate the service's TLS certificate exactly. Never accepts
/// arbitrary certificates.
/// </summary>
internal static class CertificateLoader
{
    public static X509Certificate2? LoadDevelopmentCertificate()
    {
        using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);
        foreach (var cert in store.Certificates)
        {
            if (cert.HasPrivateKey &&
                cert.Subject.Contains("CN=WinLock-Development", StringComparison.OrdinalIgnoreCase) &&
                cert.NotAfter > DateTime.UtcNow)
            {
                return new X509Certificate2(cert);
            }
        }
        return null;
    }
}