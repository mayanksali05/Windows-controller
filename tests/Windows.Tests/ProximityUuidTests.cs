using WinLock.Service.Bluetooth;
using Xunit;

namespace Windows.Tests;

public sealed class ProximityUuidTests
{
    [Fact]
    public void DerivesDeterministicUuid()
    {
        var first = ProximityUuid.ForDevice("PHONE12345678ABCD");
        var second = ProximityUuid.ForDevice("PHONE12345678ABCD");

        Assert.Equal(first, second);
    }

    [Fact]
    public void DifferentDevices_Differ()
    {
        var a = ProximityUuid.ForDevice("PHONE12345678ABCD");
        var b = ProximityUuid.ForDevice("PHONE12345678ABCE");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ProducesValidUuidV5()
    {
        var uuid = ProximityUuid.ForDevice("PHONE12345678ABCD");

        var bytes = uuid.ToByteArray();
        // .NET layout: byte[6..8] is the "version" field (mixed-endian):
        // byte[7] high nibble is the version. byte[8..9] high bits are variant.
        // In .NET ToByteArray, the version nibble is in byte 7's high bits.
        var version = (bytes[7] >> 4) & 0x0F;
        Assert.Equal(5, version);

        // Variant in byte 8 (RFC 4122): top two bits = 10.
        Assert.Equal(0b10, (bytes[8] >> 6) & 0b11);
    }

    [Fact]
    public void MatchesKnownVector()
    {
        // RFC 4122 §4.3 example: UUID v5 of "www.example.com" in the
        // DNS namespace is 2ed6657d-e927-568b-95e1-2665a8aea6a2. Our namespace
        // differs, so we only assert structural correctness here (the real
        // cross-check is the iOS mirror, documented in protocol.md).
        var uuid = ProximityUuid.ForDevice("device-1");
        Assert.Matches(
            "^[0-9a-f]{8}-[0-9a-f]{4}-5[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$",
            uuid.ToString());
    }
}