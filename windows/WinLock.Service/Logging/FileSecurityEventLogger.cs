using System.Globalization;
using System.Text.Json;

namespace WinLock.Service.Logging;

/// <summary>
/// Appends structured JSON Lines security events to
/// <c>&lt;logs&gt;/security-yyyyMMdd.jsonl</c>, rotating daily.
/// </summary>
public sealed class FileSecurityEventLogger : ISecurityEventLogger, IDisposable
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _sync = new();
    private StreamWriter? _writer;
    private string _currentDate;

    public FileSecurityEventLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        _jsonOptions = new JsonSerializerOptions();
        _currentDate = DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        _writer = new StreamWriter(Path.Combine(directory, FileName(_currentDate)), append: true)
        {
            AutoFlush = true,
        };
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

        lock (_sync)
        {
            if (!string.Equals(date, _currentDate, StringComparison.Ordinal))
            {
                Rotate(date);
            }

            _writer?.WriteLine(line);
        }
    }

    private void Rotate(string date)
    {
        _writer?.Dispose();
        _currentDate = date;
        _writer = new StreamWriter(Path.Combine(_directory, FileName(date)), append: true)
        {
            AutoFlush = true,
        };
    }

    private static string FileName(string date) => $"security-{date}.jsonl";

    public void Dispose()
    {
        lock (_sync)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}