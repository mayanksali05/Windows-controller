namespace WinLock.Service.Security;

/// <summary>Ed25519 signing/verification operations.</summary>
public interface ISigningService
{
    byte[] Sign(byte[] privateKeySeed, byte[] message);
    bool Verify(byte[] publicKey, byte[] message, byte[] signature);
}