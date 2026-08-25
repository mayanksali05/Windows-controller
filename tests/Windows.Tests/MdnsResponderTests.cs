using System.Net;
using System.Text;
using WinLock.Cryptography;
using WinLock.Service.Configuration;
using WinLock.Service.Discovery;
using Xunit;

namespace Windows.Tests;

public sealed class MdnsResponderTests
{
    private const string ServiceName = "_mywinlock._tcp.local.";

    private static MdnsResponder CreateResponder(int port = 8765)
    {
        var identity = new DeviceIdentityService(
            new DpapiSecureStorage(Path.Combine(Path.GetTempPath(), "winlock-tests", Guid.NewGuid().ToString("N"))),
            new Ed25519SigningService());
        return new MdnsResponder(new ServerOptions { Port = port }, identity);
    }

    private static byte[] BuildQuery(ushort id, string name, ushort qtype, bool unicastResponse = false)
    {
        using var ms = new MemoryStream();

        void U16(ushort value)
        {
            ms.WriteByte((byte)(value >> 8));
            ms.WriteByte((byte)value);
        }

        U16(id);
        U16(0);
        U16(1); // QDCOUNT
        U16(0);
        U16(0);
        U16(0);

        foreach (var label in name.Split('.'))
        {
            if (label.Length == 0)
            {
                continue;
            }

            ms.WriteByte((byte)label.Length);
            ms.Write(Encoding.ASCII.GetBytes(label));
        }

        ms.WriteByte(0);
        U16(qtype);
        U16((ushort)(unicastResponse ? 0x8001 : 0x0001));
        return ms.ToArray();
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return true;
            }
        }

        return false;
    }

    private static ushort AnswerCount(byte[] response) => (ushort)((response[6] << 8) | response[7]);

    [Fact]
    public void PtrQuery_ReturnsPtrAnswer()
    {
        var responder = CreateResponder();
        var query = BuildQuery(0x1234, ServiceName, 12);

        var result = responder.HandleQuery(query);

        Assert.NotNull(result);
        Assert.False(result!.Value.UnicastRequested);
        Assert.Equal(0x12, result.Value.Response[0]);
        Assert.Equal(0x34, result.Value.Response[1]);
        Assert.True(AnswerCount(result.Value.Response) >= 1);
        Assert.True(ContainsSequence(result.Value.Response, Encoding.ASCII.GetBytes("WinLock-")));
        Assert.True(ContainsSequence(result.Value.Response, new byte[] { 0x00, 0x0C })); // PTR type
    }

    [Fact]
    public void SrvQuery_ReturnsPort()
    {
        var responder = CreateResponder(port: 8888);
        var query = BuildQuery(0x0001, responder.InstanceFqdn, 33);

        var result = responder.HandleQuery(query);

        Assert.NotNull(result);
        Assert.True(ContainsSequence(result!.Value.Response, new byte[] { 0x22, 0xB8 })); // 8888
        Assert.True(ContainsSequence(result.Value.Response, new byte[] { 0x00, 0x21 })); // SRV type
        Assert.True(ContainsSequence(result.Value.Response, Encoding.ASCII.GetBytes("winlock-")));
    }

    [Fact]
    public void TxtQuery_ReturnsDeviceId()
    {
        var responder = CreateResponder();
        var query = BuildQuery(0x0002, responder.InstanceFqdn, 16);

        var result = responder.HandleQuery(query);

        Assert.NotNull(result);
        Assert.True(ContainsSequence(result!.Value.Response, Encoding.ASCII.GetBytes("device_id=")));
        Assert.True(ContainsSequence(result.Value.Response, Encoding.ASCII.GetBytes("version=1")));
    }

    [Fact]
    public void AQuery_ReturnsIpv4()
    {
        var responder = CreateResponder();
        var query = BuildQuery(0x0003, responder.HostFqdn, 1);

        var result = responder.HandleQuery(query);

        Assert.NotNull(result);
        Assert.True(AnswerCount(result!.Value.Response) >= 1);
        Assert.True(ContainsSequence(result.Value.Response, new byte[] { 0x00, 0x01 })); // A type
    }

    [Fact]
    public void UnrelatedQuery_ReturnsNull()
    {
        var responder = CreateResponder();
        var query = BuildQuery(0x0004, "_airplay._tcp.local.", 12);

        Assert.Null(responder.HandleQuery(query));
    }

    [Fact]
    public void UnicastResponseBit_IsReported()
    {
        var responder = CreateResponder();
        var query = BuildQuery(0x0005, ServiceName, 12, unicastResponse: true);

        var result = responder.HandleQuery(query);

        Assert.NotNull(result);
        Assert.True(result!.Value.UnicastRequested);
    }

    [Fact]
    public void PtrAnswer_EmbedsInstanceNameUncompressed()
    {
        var responder = CreateResponder();
        var query = BuildQuery(0x0006, ServiceName, 12);

        var result = responder.HandleQuery(query);

        Assert.NotNull(result);
        Assert.True(ContainsSequence(result!.Value.Response, Encoding.ASCII.GetBytes("WinLock-")));
        Assert.True(ContainsSequence(result.Value.Response, Encoding.ASCII.GetBytes("_mywinlock")));
    }
}