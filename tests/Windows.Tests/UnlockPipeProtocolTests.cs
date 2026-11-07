using System.Buffers.Binary;
using System.Text;
using WinLock.Service.Unlocking;
using Xunit;

namespace Windows.Tests;

/// <summary>
/// The pipe format must match windows/WinLock.CredentialProvider/common.h
/// byte for byte.
/// </summary>
public sealed class UnlockPipeProtocolTests
{
    [Fact]
    public void EncodeRequest_WritesMagicLengthAndUtf16Password()
    {
        var message = UnlockPipeProtocol.EncodeRequest("pÅss");

        Assert.Equal(new byte[] { (byte)'W', (byte)'L', (byte)'U', (byte)'1' }, message[..4]);
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(4, 4)));
        Assert.Equal("pÅss", Encoding.Unicode.GetString(message, 8, message.Length - 8));
        Assert.Equal(16, message.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a\0b")]
    public void EncodeRequest_RejectsInvalidPasswords(string password)
    {
        Assert.Throws<ArgumentException>(() => UnlockPipeProtocol.EncodeRequest(password));
    }

    [Fact]
    public void EncodeRequest_RejectsOverlongPasswords()
    {
        Assert.Throws<ArgumentException>(() =>
            UnlockPipeProtocol.EncodeRequest(new string('x', UnlockPipeProtocol.MaxPasswordChars + 1)));
    }

    [Fact]
    public void DecodeResponse_ReadsCodeAndStatus()
    {
        var response = new byte[] { 1, 0x6D, 0x00, 0x00, 0xC0 }; // REJECTED, STATUS_LOGON_FAILURE

        Assert.True(UnlockPipeProtocol.TryDecodeResponse(response, out var code, out var status));
        Assert.Equal(ProviderResultCode.Rejected, code);
        Assert.Equal(unchecked((int)0xC000006D), status);
    }

    [Theory]
    [InlineData(new byte[] { 0, 0, 0, 0 })]
    [InlineData(new byte[] { 9, 0, 0, 0, 0 })]
    public void DecodeResponse_RejectsMalformedResponses(byte[] response)
    {
        Assert.False(UnlockPipeProtocol.TryDecodeResponse(response, out _, out _));
    }
}
