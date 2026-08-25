using System.Net;
using System.Text;

namespace WinLock.Service.Discovery;

/// <summary>A parsed DNS question.</summary>
internal readonly record struct DnsQuestion(string Name, ushort Type, ushort Class);

/// <summary>
/// Minimal DNS/mDNS wire format helpers (RFC 1035 §4, RFC 6762). Only the
/// subset needed by the WinLock responder is implemented: query parsing and
/// building PTR/SRV/TXT/A answers. Names are encoded uncompressed, which
/// mDNS clients (including Apple's dns_sd used by iOS NWBrowser) accept.
/// </summary>
internal static class DnsWire
{
    public const ushort TypePtr = 12;
    public const ushort TypeTxt = 16;
    public const ushort TypeSrv = 33;
    public const ushort TypeA = 1;
    public const ushort ClassIn = 1;
    public const uint DefaultTtl = 120;

    private const int HeaderSize = 12;

    public static IReadOnlyList<DnsQuestion> ParseQuestions(byte[] packet, int length, out ushort id)
    {
        id = 0;
        var questions = new List<DnsQuestion>();
        if (length < HeaderSize)
        {
            return questions;
        }

        id = (ushort)((packet[0] << 8) | packet[1]);
        var qdCount = (packet[4] << 8) | packet[5];
        var offset = HeaderSize;

        for (var i = 0; i < qdCount && offset < length; i++)
        {
            if (!TryReadName(packet, ref offset, out var name) || offset + 4 > length)
            {
                break;
            }

            var qtype = (ushort)((packet[offset] << 8) | packet[offset + 1]);
            var qclass = (ushort)((packet[offset + 2] << 8) | packet[offset + 3]);
            offset += 4;

            questions.Add(new DnsQuestion(name, qtype, qclass));
        }

        return questions;
    }

    public static byte[] BuildResponse(ushort id, IReadOnlyList<byte[]> answers)
    {
        using var ms = new MemoryStream();
        WriteUInt16(ms, id);
        WriteUInt16(ms, 0x8400); // QR=response, AA
        WriteUInt16(ms, 0);      // QDCOUNT (answers only)
        WriteUInt16(ms, (ushort)answers.Count);
        WriteUInt16(ms, 0);      // NSCOUNT
        WriteUInt16(ms, 0);      // ARCOUNT

        foreach (var answer in answers)
        {
            ms.Write(answer);
        }

        return ms.ToArray();
    }

    public static byte[] PtrAnswer(string questionName, string target)
        => BuildRecord(questionName, TypePtr, EncodeName(target));

    public static byte[] SrvAnswer(string instanceName, string targetHost, ushort port)
    {
        var target = EncodeName(targetHost);
        var rdata = new byte[6 + target.Length];
        rdata[4] = (byte)(port >> 8);
        rdata[5] = (byte)port;
        target.CopyTo(rdata, 6);
        return BuildRecord(instanceName, TypeSrv, rdata);
    }

    public static byte[] TxtAnswer(string instanceName, IReadOnlyDictionary<string, string> txt)
    {
        var strings = new List<byte>();
        foreach (var (key, value) in txt)
        {
            var s = Encoding.ASCII.GetBytes($"{key}={value}");
            if (s.Length > 255)
            {
                s = s[..255];
            }

            strings.Add((byte)s.Length);
            strings.AddRange(s);
        }

        return BuildRecord(instanceName, TypeTxt, strings.ToArray());
    }

    public static byte[] AAnswer(string hostName, IPAddress address)
        => BuildRecord(hostName, TypeA, address.GetAddressBytes());

    private static byte[] BuildRecord(string name, ushort type, byte[] rdata)
    {
        var nameBytes = EncodeName(name);
        var record = new byte[nameBytes.Length + 10 + rdata.Length];
        var offset = 0;

        nameBytes.CopyTo(record, offset);
        offset += nameBytes.Length;

        record[offset++] = (byte)(type >> 8);
        record[offset++] = (byte)type;
        record[offset++] = 0;       // CLASS IN high
        record[offset++] = (byte)ClassIn; // CLASS IN low
        offset += 4;                // TTL = DefaultTtl (4 bytes, all zero except last)
        record[offset - 1] = (byte)DefaultTtl;
        record[offset++] = (byte)(rdata.Length >> 8);
        record[offset++] = (byte)rdata.Length;
        rdata.CopyTo(record, offset);

        return record;
    }

    private static byte[] EncodeName(string dottedName)
    {
        using var ms = new MemoryStream();
        foreach (var label in dottedName.Split('.'))
        {
            if (label.Length == 0)
            {
                continue;
            }

            if (label.Length > 63)
            {
                throw new ArgumentException($"DNS label too long: {label}", nameof(dottedName));
            }

            ms.WriteByte((byte)label.Length);
            ms.Write(Encoding.ASCII.GetBytes(label));
        }

        ms.WriteByte(0);
        return ms.ToArray();
    }

    private static bool TryReadName(byte[] packet, ref int offset, out string name)
    {
        name = string.Empty;
        var labels = new List<string>();
        var cursor = offset;
        var bytesRead = 0;
        var jumped = false;

        while (true)
        {
            if (cursor >= packet.Length)
            {
                return false;
            }

            var length = packet[cursor];
            if ((length & 0xC0) == 0xC0)
            {
                if (cursor + 1 >= packet.Length)
                {
                    return false;
                }

                if (!jumped)
                {
                    bytesRead = cursor - offset + 2;
                    jumped = true;
                }

                cursor = ((length & 0x3F) << 8) | packet[cursor + 1];
                continue;
            }

            if (length == 0)
            {
                cursor++;
                if (!jumped)
                {
                    bytesRead = cursor - offset;
                }

                break;
            }

            if (cursor + 1 + length > packet.Length)
            {
                return false;
            }

            cursor++;
            labels.Add(Encoding.ASCII.GetString(packet, cursor, length));
            cursor += length;
        }

        if (!jumped)
        {
            offset += bytesRead;
        }
        else
        {
            offset = offset + bytesRead;
        }

        name = labels.Count == 0 ? "." : string.Join(".", labels) + ".";
        return true;
    }

    private static void WriteUInt16(Stream stream, ushort value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }
}