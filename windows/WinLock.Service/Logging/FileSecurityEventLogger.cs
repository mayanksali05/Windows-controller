using System.Globalization;
using System.Text.Json;

namespace WinLock.Service.Logging;

/// <summary>
/// Appends structured JSON Lines security events to
/// <c>&lt;logs&gt;/security-yyyyMMdd.jsonl</c>, rotating daily. The log file is
/// opened with <see cref="FileShare.ReadWrite"/> and writes are serialized
/// process-wide so overlapping instances/hosts never crash the logger.
/// </summary>
public sealed class FileSecurityEventLogger : ISecurityEventLogger, IDisposable
{
    private static readonly object GlobalSync = new();

    private readonly string _directory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _instanceSync = new();
    private StreamWriter? _writer;
    private string _currentDate;

    public FileSecurityEventLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        _jsonOptions = new JsonSerializerOptions();
        _currentDate = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        _writer = OpenWriter(Path.Combine(directory, FileName(_currentDate)));
    }

    public void Log(SecurityEventType type, string message, object? data = null)
    {
        var utcNow = DateTime.UtcNow;
        var date = utcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var entry = new Dictionary<string, object?>
        {
            ["timestamp"] = utcNow.ToString("O", CultureInfo.InvariantCulture),
            ["event"] = type.ToString(),
            ["message"] = message,
            ["data"] = SensitiveDataGuard.Scrub(data),
        };

        var line = JsonSerializer.Serialize(entry, _jsonOptions);

        lock (GlobalSync)
        {
            lock (_instanceSync)
            {
                if (!string.Equals(date, _currentDate, StringComparison.Ordinal))
                {
                    Rotate(date);
                }

                _writer?.WriteLine(line);
            }
        }
    }

    private void Rotate(string date)
    {
        _writer?.Dispose();
        _currentDate = date;
        _writer = OpenWriter(Path.Combine(_directory, FileName(date)));
    }

    private static StreamWriter OpenWriter(string path) =>
        new(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            AutoFlush = true,
        };

    private static string FileName(string date) => $"security-{date}.jsonl";

    public void Dispose()
    {
        lock (GlobalSync)
        {
            lock (_instanceSync)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}