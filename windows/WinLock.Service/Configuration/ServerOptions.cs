namespace WinLock.Service.Configuration;

/// <summary>Server binding and behavior options (<c>Server</c> section).</summary>
public sealed class ServerOptions
{
    public const string SectionName = "Server";

    public int Port { get; set; } = 8765;
    public string Environment { get; set; } = "Development";
    public bool UseHttps { get; set; } = true;
    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>Optional certificate thumbprint; falls back to the CN=WinLock-Development cert.</summary>
    public string CertificateThumbprint { get; set; } = string.Empty;

    public int? RateLimitPermitsPerWindow { get; set; } = 60;
    public int RateLimitWindowSeconds { get; set; } = 60;
}