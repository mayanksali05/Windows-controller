using WinLock.Cryptography;
using WinLock.Protocol;

namespace WinLock.Service.Security;

/// <summary>
/// Central authorization check: a device is authorized if it is a paired
/// iPhone (in the authorized-device store) or the Windows laptop itself (its
/// own identity, used by the tray application).
/// </summary>
public sealed class DeviceAuthorizer
{
    private readonly DeviceIdentityService _identity;
    private readonly AuthorizedDeviceStore _devices;

    public DeviceAuthorizer(DeviceIdentityService identity, AuthorizedDeviceStore devices)
    {
        _identity = identity;
        _devices = devices;
    }

    public bool IsAuthorized(string deviceId) =>
        _devices.IsPaired(deviceId) ||
        string.Equals(deviceId, _identity.DeviceId, StringComparison.Ordinal);

    /// <summary>Returns the Ed25519 public key for a device, or null if unauthorized.</summary>
    public byte[]? GetPublicKey(string deviceId)
    {
        if (string.Equals(deviceId, _identity.DeviceId, StringComparison.Ordinal))
        {
            return _identity.PublicKey;
        }

        var device = _devices.GetByDeviceId(deviceId);
        return device is null ? null : Base64Url.Decode(device.PublicKeyBase64Url);
    }
}