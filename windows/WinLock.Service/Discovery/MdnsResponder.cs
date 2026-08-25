using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using WinLock.Cryptography;
using WinLock.Service.Configuration;

namespace WinLock.Service.Discovery;

/// <summary>
/// Dependency-free mDNS responder that advertises <c>_mywinlock._tcp</c> on the
/// LAN (RFC 6762). Listens on UDP 5353 (multicast + unicast) and answers
/// PTR/SRV/TXT/A queries so the iPhone's NWBrowser can discover the laptop
/// without a fixed IP. Discovery is a convenience, never a trust signal.
/// </summary>
public sealed class MdnsResponder : IDiscoveryAdvertiser, IDisposable
{
    private const int MdnsPort = 5353;
    private static readonly IPAddress MulticastGroup = IPAddress.Parse("224.0.0.251");

    private readonly string _serviceName;   // "_mywinlock._tcp.local."
    private readonly string _instanceFqdn;  // "WinLock-XXXXXX._mywinlock._tcp.local."
    private readonly string _hostName;      // "winlock-xxxxxx.local."
    private readonly ushort _port;
    private readonly int _listenPort;
    private readonly IReadOnlyDictionary<string, string> _txt;

    private UdpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;

    public MdnsResponder(ServerOptions options, DeviceIdentityService identity, int? listenPort = null)
    {
        var suffix = identity.DeviceId[..6];
        _serviceName = "_mywinlock._tcp.local.";
        _instanceFqdn = $"WinLock-{suffix}.{_serviceName}";
        _hostName = $"winlock-{suffix.ToLowerInvariant()}.local.";
        _port = (ushort)options.Port;
        _listenPort = listenPort ?? MdnsPort;
        _txt = new Dictionary<string, string>
        {
            ["device_id"] = identity.DeviceId,
            ["version"] = "1",
        };
    }

    /// <summary>The advertised instance FQDN, e.g. <c>WinLock-ABC123._mywinlock._tcp.local.</c>.</summary>
    internal string InstanceFqdn => _instanceFqdn;

    /// <summary>The advertised host FQDN used by SRV/A records, e.g. <c>winlock-abc123.local.</c>.</summary>
    internal string HostFqdn => _hostName;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _client = new UdpClient();
        _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _client.Client.Bind(new IPEndPoint(IPAddress.Any, _listenPort));

        foreach (var interfaceAddress in MulticastInterfaces())
        {
            try
            {
                _client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                    new MulticastOption(MulticastGroup, interfaceAddress));
            }
            catch (SocketException)
            {
                // Interface may not support multicast; skip it.
            }
        }

        _cts = new CancellationTokenSource();
        _receiveLoop = ReceiveLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop.ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Swallow shutdown noise.
            }
        }

        _client?.Dispose();
        _client = null;
    }

    public void Dispose() => _client?.Dispose();

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _client!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var handled = HandleQuery(result.Buffer);
            if (handled is null || _client is null)
            {
                continue;
            }

            var response = handled.Value.Response;
            var unicastRequested = handled.Value.UnicastRequested;

            if (unicastRequested)
            {
                _client.Send(response, response.Length, result.RemoteEndPoint);
            }
            else
            {
                try
                {
                    _client.Send(response, response.Length, new IPEndPoint(MulticastGroup, MdnsPort));
                }
                catch (SocketException)
                {
                    _client.Send(response, response.Length, result.RemoteEndPoint);
                }
            }
        }
    }

    /// <summary>
    /// Parses a DNS query and returns the mDNS response bytes (or null when the
    /// query does not target this service). Separated from the socket loop so it
    /// is unit-testable. <paramref name="unicastRequested"/> is true when the
    /// query asked for a unicast response (QU bit).
    /// </summary>
    internal (byte[] Response, bool UnicastRequested)? HandleQuery(byte[] packet)
    {
        var questions = DnsWire.ParseQuestions(packet, packet.Length, out var id);
        if (questions.Count == 0)
        {
            return null;
        }

        var answers = new List<byte[]>();
        var unicastRequested = false;

        foreach (var question in questions)
        {
            if ((question.Class & 0x8000) != 0)
            {
                unicastRequested = true;
            }

            var qclass = (ushort)(question.Class & 0x7FFF);
            if (qclass != DnsWire.ClassIn)
            {
                continue;
            }

            if (string.Equals(question.Name, _serviceName, StringComparison.OrdinalIgnoreCase) &&
                question.Type == DnsWire.TypePtr)
            {
                answers.Add(DnsWire.PtrAnswer(question.Name, _instanceFqdn));
            }
            else if (string.Equals(question.Name, _instanceFqdn, StringComparison.OrdinalIgnoreCase) &&
                     question.Type == DnsWire.TypeSrv)
            {
                answers.Add(DnsWire.SrvAnswer(question.Name, _hostName, _port));
            }
            else if (string.Equals(question.Name, _instanceFqdn, StringComparison.OrdinalIgnoreCase) &&
                     question.Type == DnsWire.TypeTxt)
            {
                answers.Add(DnsWire.TxtAnswer(question.Name, _txt));
            }
            else if (string.Equals(question.Name, _hostName, StringComparison.OrdinalIgnoreCase) &&
                     question.Type == DnsWire.TypeA)
            {
                foreach (var address in LocalIpv4Addresses())
                {
                    answers.Add(DnsWire.AAnswer(question.Name, address));
                }
            }
        }

        return answers.Count == 0
            ? null
            : (DnsWire.BuildResponse(id, answers), unicastRequested);
    }

    private static IEnumerable<IPAddress> MulticastInterfaces()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return unicast.Address;
                }
            }
        }
    }

    private static IEnumerable<IPAddress> LocalIpv4Addresses()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    yield return unicast.Address;
                }
            }
        }
    }
}