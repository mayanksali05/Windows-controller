namespace WinLock.Cryptography;

/// <summary>
/// Abstraction over OS-protected secret storage (DPAPI in the default
/// implementation). Secrets are never written in plaintext.
/// </summary>
public interface ISecureStorage
{
    void Save(string name, byte[] data);
    byte[]? Load(string name);
    bool Delete(string name);
}