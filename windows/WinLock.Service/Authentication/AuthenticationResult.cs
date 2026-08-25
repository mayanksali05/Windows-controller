namespace WinLock.Service.Authentication;

/// <summary>Result of an authentication attempt.</summary>
/// <param name="Success">True when the request is authenticated.</param>
/// <param name="FailureCode">Protocol error code when <paramref name="Success"/> is false.</param>
/// <param name="FailureMessage">Generic message safe to send to the client.</param>
public sealed record AuthenticationResult(bool Success, string? FailureCode = null, string? FailureMessage = null)
{
    public static AuthenticationResult Ok() => new(true);

    public static AuthenticationResult Fail(string code, string message) =>
        new(false, code, message);
}