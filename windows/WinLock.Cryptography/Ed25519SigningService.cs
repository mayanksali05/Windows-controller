namespace WinLock.Cryptography;

public sealed class Ed25519SigningService : ISigningService
{
    public byte[] Sign(byte[] privateKeySeed, byte[] message) =>
        Ed25519.Sign(privateKeySeed, message);

    public bool Verify(byte[] publicKey, byte[] message, byte[] signature) =>
        Ed25519.Verify(publicKey, message, signature);
}