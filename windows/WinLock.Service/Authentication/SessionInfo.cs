namespace WinLock.Service.Authentication;

/// <summary>A short-lived authenticated session.</summary>
public sealed record SessionInfo(string Token, string DeviceId, DateTimeOffset ExpiresAtUtc);