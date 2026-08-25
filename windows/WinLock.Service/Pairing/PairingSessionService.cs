using System.Collections.Concurrent;
using System.Security.Cryptography;
using WinLock.Protocol;

namespace WinLock.Service.Pairing;

/// <summary>
/// Creates and consumes one-time pairing sessions. Sessions are in-memory and
/// single-use: a token is atomically removed on first consumption, so a replay
/// of <c>pair/confirm</c> is rejected. Tokens and nonces come from a CSPRNG.
/// </summary>
public sealed class PairingSessionService
{
    private readonly TimeSpan _tokenLifetime;
    private readonly ConcurrentDictionary<string, PairingSession> _sessions = new();

    public PairingSessionService(TimeSpan tokenLifetime)
    {
        _tokenLifetime = tokenLifetime;
    }

    public PairingSession Create()
    {
        var session = new PairingSession
        {
            Token = Base64Url.Encode(RandomNumberGenerator.GetBytes(32)),
            Nonce = Base64Url.Encode(RandomNumberGenerator.GetBytes(32)),
            ExpiresAtUtc = DateTimeOffset.UtcNow.Add(_tokenLifetime),
        };
        _sessions[session.Token] = session;
        return session;
    }

    /// <summary>Atomically removes and returns the session, or reports expired/absent.</summary>
    public PairingConsumeResult TryConsume(string token)
    {
        if (!_sessions.TryRemove(token, out var session))
        {
            return PairingConsumeResult.NotFound();
        }

        return session.IsExpired
            ? PairingConsumeResult.TimedOut()
            : PairingConsumeResult.Success(session);
    }

    /// <summary>Returns the first unexpired session, if any (for /pair/request).</summary>
    public PairingSession? GetActive()
    {
        foreach (var session in _sessions.Values)
        {
            if (!session.IsExpired)
            {
                return session;
            }
        }

        return null;
    }

    public void CleanupExpired()
    {
        foreach (var (token, session) in _sessions)
        {
            if (session.IsExpired)
            {
                _sessions.TryRemove(token, out _);
            }
        }
    }
}