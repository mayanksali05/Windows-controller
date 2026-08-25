using System.Security.Cryptography;

namespace WinLock.Cryptography;

/// <summary>
/// File-backed store whose blobs are encrypted with DPAPI (CurrentUser scope),
/// tying decryption to the service account on this machine. Files are stored
/// in the supplied directory. Writes are serialized process-wide so concurrent
/// holders (service + tray) cannot tear or corrupt blobs.
/// </summary>
public sealed class DpapiSecureStorage : ISecureStorage
{
    private static readonly object GlobalSync = new();

    private readonly string _directory;

    public DpapiSecureStorage(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Save(string name, byte[] data)
    {
        var protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        var path = Path.Combine(_directory, SafeName(name));

        lock (GlobalSync)
        {
            File.WriteAllBytes(path, protectedData);
        }
    }

    public byte[]? Load(string name)
    {
        var path = Path.Combine(_directory, SafeName(name));
        lock (GlobalSync)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var protectedData = File.ReadAllBytes(path);
            return ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
        }
    }

    public bool Delete(string name)
    {
        var path = Path.Combine(_directory, SafeName(name));
        lock (GlobalSync)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return safe + ".dpapi";
    }
}