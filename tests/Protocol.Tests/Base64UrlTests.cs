using WinLock.Protocol;
using Xunit;

namespace Protocol.Tests;

public sealed class Base64UrlTests
{
    [Theory]
    [InlineData(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 })]
    [InlineData(new byte[] { 0xff, 0xfe, 0xfd })]
    [InlineData(new byte[] { 0x73, 0x69, 0x67, 0x6e }) ]
    public void RoundTrip(byte[] data)
    {
        var encoded = Base64Url.Encode(data);
        Assert.DoesNotContain(encoded, c => c is '+' or '/' or '=');
        Assert.Equal(data, Base64Url.Decode(encoded));
    }

    [Fact]
    public void Empty_RoundTrips()
    {
        Assert.Equal(string.Empty, Base64Url.Encode(Array.Empty<byte>()));
        Assert.Empty(Base64Url.Decode(string.Empty));
    }

    [Fact]
    public void Decode_ToleratesStandardBase64()
    {
        var standard = Convert.ToBase64String(new byte[] { 0xfb, 0xff, 0x00 });
        var decoded = Base64Url.Decode(standard);
        Assert.Equal(new byte[] { 0xfb, 0xff, 0x00 }, decoded);
    }
}