namespace WinLock.Protocol.Models;

/// <summary>Structured error payload. Messages never expose implementation details.</summary>
public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}