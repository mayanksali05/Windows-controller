using System.Text;

namespace WinLock.Protocol;

/// <summary>
/// Deterministic byte sequences that are signed by Ed25519 (RFC 8032). Both the
/// Windows service and the iPhone app must produce byte-for-byte identical
/// inputs; the Swift side re-implements these exact rules.
/// </summary>
public static class ProtocolStrings
{
    public const string ChallengeVerifyEndpoint = "/api/v1/auth/verify";
    public const string PairingConfirmEndpoint = "/api/v1/pair/confirm";

    /// <summary>Input for Phase 4 challenge-response signing (client side).</summary>
    public static byte[] AuthenticationSigningInput(string clientDeviceId, string challenge, string timestamp, string endpoint)
        => Canonical(clientDeviceId, challenge, timestamp, endpoint);

    /// <summary>Input for pairing signatures: device_id ‖ nonce.</summary>
    public static byte[] PairingSigningInput(string deviceId, string nonce)
        => Encoding.UTF8.GetBytes($"{deviceId}\u001f{nonce}\u001e");

    private static byte[] Canonical(string a, string b, string c, string d)
        => Encoding.UTF8.GetBytes($"{a}\u001f{b}\u001f{c}\u001f{d}\u001e");
}