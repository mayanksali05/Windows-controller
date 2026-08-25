using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace WinLock.Service.Security;

/// <summary>
/// Ed25519 (RFC 8032) primitives, interoperable with CryptoKit's
/// Curve25519.Signing on the iPhone. Signatures are deterministic and use
/// raw 32-byte public keys and 64-byte signatures.
/// </summary>
public static class Ed25519
{
    public const int PublicKeySize = 32;
    public const int SignatureSize = 64;

    public static byte[] GeneratePrivateKeySeed()
    {
        var key = new Ed25519PrivateKeyParameters(new SecureRandom());
        return key.GetEncoded();
    }

    public static byte[] DerivePublicKey(byte[] privateKeySeed)
    {
        var key = new Ed25519PrivateKeyParameters(privateKeySeed, 0);
        return key.GeneratePublicKey().GetEncoded();
    }

    public static byte[] Sign(byte[] privateKeySeed, byte[] message)
    {
        var key = new Ed25519PrivateKeyParameters(privateKeySeed, 0);
        var signer = new Ed25519Signer();
        signer.Init(true, key);
        signer.BlockUpdate(message, 0, message.Length);
        return signer.GenerateSignature();
    }

    public static bool Verify(byte[] publicKey, byte[] message, byte[] signature)
    {
        if (publicKey.Length != PublicKeySize || signature.Length != SignatureSize)
        {
            return false;
        }

        try
        {
            var key = new Ed25519PublicKeyParameters(publicKey, 0);
            var signer = new Ed25519Signer();
            signer.Init(false, key);
            signer.BlockUpdate(message, 0, message.Length);
            return signer.VerifySignature(signature);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}