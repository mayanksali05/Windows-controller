namespace WinLock.Protocol.Models;

/// <summary>
/// Body of <c>POST /api/v1/unlock</c>. The password is the user's real Windows
/// account password, stored on the iPhone (Keychain, released by Face ID) and
/// sent only over the pinned TLS session. The service never stores or logs it.
/// </summary>
public sealed class UnlockRequestDto
{
    /// <summary>Optional client device identifier, used for audit logging only.</summary>
    public string? DeviceId { get; init; }

    public string? Password { get; init; }

    /// <summary>Never print the password (e.g. in exception messages or debuggers).</summary>
    public override string ToString() => $"UnlockRequestDto {{ DeviceId = {DeviceId}, Password = *** }}";
}
