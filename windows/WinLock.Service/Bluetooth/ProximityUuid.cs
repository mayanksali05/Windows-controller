using System.Security.Cryptography;
using System.Text;

namespace WinLock.Service.Bluetooth;

/// <summary>
/// Deterministic BLE service UUID for a device (RFC 4122 v5). The iPhone
/// advertises <see cref="ForDevice"/> of its own device id; the Windows scanner
/// derives the same UUID for each paired device, so a phone can be identified
/// by the service UUID it advertises without connecting. The iOS app mirrors
/// this derivation byte-for-byte.
/// </summary>
public static class ProximityUuid
{
    public static readonly Guid Namespace = Guid.Parse("9B2F6D21-8E4C-4E2A-9F6A-9D4E3B2C1A00");

    public static Guid ForDevice(string deviceId)
    {
        var namespaceBytes = Namespace.ToByteArray(); // .NET mixed-endian layout
        SwapEndianness(namespaceBytes);               // to RFC 4122 network order

        var name = Encoding.UTF8.GetBytes(deviceId);
        var buffer = new byte[16 + name.Length];
        namespaceBytes.CopyTo(buffer, 0);
        name.CopyTo(buffer, 16);

        var digest = SHA1.HashData(buffer); // 20 bytes
        var bytes = new byte[16];
        Array.Copy(digest, bytes, 16);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant

        // Format from the network-order bytes so the result matches the iOS
        // derivation exactly (canonical hex string → Guid.Parse).
        return Guid.Parse(FormatHex(bytes));
    }

    private static string FormatHex(byte[] bytes)
    {
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"{hex[..8]}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex.Substring(20, 12)}";
    }

    private static void SwapEndianness(byte[] bytes)
    {
        Array.Reverse(bytes, 0, 4);
        Array.Reverse(bytes, 4, 2);
        Array.Reverse(bytes, 6, 2);
    }
}