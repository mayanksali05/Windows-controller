namespace WinLock.Protocol.Models;

/// <summary>Body of <c>POST /api/v1/lock</c>.</summary>
public sealed class LockRequest
{
    /// <summary>Optional client device identifier, used for audit logging only.</summary>
    public string? DeviceId { get; init; }
}