using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace WinLock.Service.Logging;

/// <summary>
/// Removes properties whose names indicate sensitive material (tokens, keys,
/// passwords, secrets, signatures, nonces) before logging. Defense in depth:
/// security-critical values should never be passed to the logger at all.
/// </summary>
public static partial class SensitiveDataGuard
{
    [GeneratedRegex("(token|password|secret|privatekey|signature|nonce|pin|credential)",
        RegexOptions.IgnoreCase)]
    private static partial Regex SensitivePropertyRegex();

    public static IReadOnlyDictionary<string, object?> Scrub(object? data)
    {
        if (data is null)
        {
            return new Dictionary<string, object?>();
        }

        var result = new Dictionary<string, object?>();

        switch (data)
        {
            case IReadOnlyDictionary<string, object?> typed:
                foreach (var (key, value) in typed)
                {
                    if (!IsSensitive(key))
                    {
                        result[key] = value;
                    }
                }
                return result;

            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key?.ToString() ?? string.Empty;
                    if (!IsSensitive(key))
                    {
                        result[key] = entry.Value;
                    }
                }
                return result;

            default:
                foreach (var property in data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!IsSensitive(property.Name))
                    {
                        result[property.Name] = property.GetValue(data);
                    }
                }
                return result;
        }
    }

    private static bool IsSensitive(string name) => SensitivePropertyRegex().IsMatch(name);
}