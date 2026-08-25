namespace WinLock.Service.Logging;

/// <summary>
/// Structured security-event sink. Implementations must never log private keys,
/// passwords, tokens, or raw cryptographic secrets.
/// </summary>
public interface ISecurityEventLogger
{
    void Log(SecurityEventType type, string message, object? data = null);
}