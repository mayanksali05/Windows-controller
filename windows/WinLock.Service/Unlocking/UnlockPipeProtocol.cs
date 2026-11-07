using System.Buffers.Binary;
using System.Text;

namespace WinLock.Service.Unlocking;

/// <summary>Result codes returned by the credential provider over the pipe.</summary>
public enum ProviderResultCode : byte
{
    Success = 0,
    Rejected = 1,
    NoUser = 2,
    Timeout = 3,
    BadRequest = 4,
    Busy = 5,
}

/// <summary>
/// Wire format shared with <c>windows/WinLock.CredentialProvider</c>
/// (see common.h there). All integers little-endian.
/// <code>
/// request : "WLU1" | uint32 passwordByteCount | UTF-16LE password
/// response: uint8 code | int32 NTSTATUS
/// </code>
/// </summary>
public static class UnlockPipeProtocol
{
    public const string PipeName = "WinLockUnlock";
    public const uint Magic = 0x31554C57; // "WLU1"
    public const int MaxPasswordChars = 256;
    public const int ResponseLength = 5;

    /// <summary>Encodes a request. The caller must zero the returned buffer after use.</summary>
    public static byte[] EncodeRequest(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0 || password.Length > MaxPasswordChars)
        {
            throw new ArgumentException("Password length is out of range.", nameof(password));
        }
        if (password.Contains('\0'))
        {
            throw new ArgumentException("Password contains a NUL character.", nameof(password));
        }

        var passwordBytes = Encoding.Unicode.GetByteCount(password);
        var message = new byte[8 + passwordBytes];
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(0, 4), Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4, 4), (uint)passwordBytes);
        Encoding.Unicode.GetBytes(password, message.AsSpan(8));
        return message;
    }

    public static bool TryDecodeResponse(ReadOnlySpan<byte> response, out ProviderResultCode code, out int ntStatus)
    {
        code = ProviderResultCode.BadRequest;
        ntStatus = 0;
        if (response.Length != ResponseLength || response[0] > (byte)ProviderResultCode.Busy)
        {
            return false;
        }

        code = (ProviderResultCode)response[0];
        ntStatus = BinaryPrimitives.ReadInt32LittleEndian(response.Slice(1, 4));
        return true;
    }
}
