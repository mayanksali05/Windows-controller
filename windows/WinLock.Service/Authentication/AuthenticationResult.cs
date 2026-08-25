namespace WinLock.Service.Authentication;

/// <summary>Result of an authentication attempt.</summary>
/// <param name="Success">True when the request is authenticated.</param>
/// <param name="FailureCode">Protocol error code when <paramref name="Success"/> is false.</param>
/// <param name="FailureMessage">Generic message safe to send to the client.</param>
/// <param name="DeviceId">Authenticated device id when <paramref name="Success"/> is true.</param>
public sealed record AuthenticationResult(
    bool Success,
    string? FailureCode = null,
    string? FailureMessage = null,
    string? DeviceId = null)
{
    public static AuthenticationResult Ok(string deviceId) => new(true, DeviceId: deviceId);

    public static AuthenticationResult Fail(string code, string message) =>
        new(false, code, message);
}