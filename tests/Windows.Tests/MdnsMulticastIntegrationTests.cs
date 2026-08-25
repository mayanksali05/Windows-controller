using System.Net;
using System.Net.Sockets;
using System.Text;
using WinLock.Cryptography;
using WinLock.Service.Configuration;
using WinLock.Service.Discovery;
using Xunit;

namespace Windows.Tests;

/// <summary>
/// Exercises the mDNS responder's full socket loop (receive query → parse →
/// build response → send reply) with a real UDP pair on a private port, using
/// the QU bit so the response comes back unicast (avoids same-host multicast
/// loopback quirks on Windows). Best-effort network test.
/// </summary>
public sealed class MdnsMulticastIntegrationTests
{
    private const int TestPort = 15353;

    [Fact]
    public async Task Responds_ToQuery_OverRealSocket()
    {
        var responder = new MdnsResponder(
            new ServerOptions { Port = 8765 },
            new DeviceIdentityService(
                new DpapiSecureStorage(Path.Combine(Path.GetTempPath(), "winlock-tests", Guid.NewGuid().ToString("N"))),
                new Ed25519SigningService()),
            listenPort: TestPort);

        try
        {
            await responder.StartAsync(CancellationToken.None);

            using var client = new UdpClient();
            client.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            client.Client.ReceiveTimeout = 5000;

            client.Send(BuildPtrQuery(unicastResponse: true), new IPEndPoint(IPAddress.Loopback, TestPort));

            var endpoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] response;
            try
            {
                response = client.Receive(ref endpoint);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                Assert.Fail("No mDNS response received within 5s.");
                return;
            }

            var text = Encoding.ASCII.GetString(response);
            Assert.Contains("WinLock-", text);
            Assert.Equal(0x22, response[0]);
            Assert.Equal(0x22, response[1]);
        }
        finally
        {
            await responder.StopAsync(CancellationToken.None);
        }
    }

    private static byte[] BuildPtrQuery(bool unicastResponse)
    {
        using var ms = new MemoryStream();

        void U16(ushort value)
        {
            ms.WriteByte((byte)(value >> 8));
            ms.WriteByte((byte)value);
        }

        U16(0x2222);
        U16(0);
        U16(1);
        U16(0);
        U16(0);
        U16(0);

        foreach (var label in "_mywinlock._tcp.local.".Split('.'))
        {
            if (label.Length == 0)
            {
                continue;
            }

            ms.WriteByte((byte)label.Length);
            ms.Write(Encoding.ASCII.GetBytes(label));
        }

        ms.WriteByte(0);
        U16(12); // PTR
        U16((ushort)(unicastResponse ? 0x8001 : 0x0001));
        return ms.ToArray();
    }
}