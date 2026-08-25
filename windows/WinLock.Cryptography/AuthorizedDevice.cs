namespace WinLock.Cryptography;

/// <summary>A successfully paired client device.</summary>
public sealed class AuthorizedDevice
{
    public string DeviceId { get; init; } = string.Empty;
    public string PublicKeyBase64Url { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset PairedAtUtc { get; init; }
}