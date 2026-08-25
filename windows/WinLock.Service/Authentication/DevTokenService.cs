using System.Security.Cryptography;
using System.Text;

namespace WinLock.Service.Authentication;

/// <summary>
/// Generates and validates the development bearer token. The token is a fresh
/// cryptographically random value created at startup (never hardcoded) and kept
/// only in memory. DEVELOPMENT-ONLY; replaced by challenge-response in Phase 4.
/// </summary>
public sealed class DevTokenService
{
    private readonly byte[] _tokenBytes;
    private readonly string _token;

    public DevTokenService()
    {
        _tokenBytes = RandomNumberGenerator.GetBytes(32);
        _token = Convert.ToHexString(_tokenBytes);
    }

    public string Token => _token;

    /// <summary>Constant-time comparison against the generated token.</summary>
    public bool Validate(string? candidate)
    {
        if (string.IsNullOrEmpty(candidate))
        {
            return false;
        }

        var candidateBytes = Encoding.UTF8.GetBytes(candidate);
        var tokenBytes = Encoding.UTF8.GetBytes(_token);
        if (candidateBytes.Length != tokenBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(candidateBytes, tokenBytes);
    }
}