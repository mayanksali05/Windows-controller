using System.Security.Cryptography;

namespace WinLock.Service.Security;

/// <summary>
/// File-backed store whose blobs are encrypted with DPAPI (CurrentUser scope),
/// tying decryption to the service account on this machine. Files are stored
/// under <c>%LOCALAPPDATA%\WinLock\storage</c> unless a directory is supplied.
/// </summary>
public sealed class DpapiSecureStorage : ISecureStorage
{
    private readonly string _directory;
    private readonly object _sync = new();

    public DpapiSecureStorage(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Save(string name, byte[] data)
    {
        var protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        lock (_sync)
        {
            File.WriteAllBytes(Path.Combine(_directory, SafeName(name)), protectedData);
        }
    }

    public byte[]? Load(string name)
    {
        var path = Path.Combine(_directory, SafeName(name));
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedData = File.ReadAllBytes(path);
        return ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
    }

    public bool Delete(string name)
    {
        var path = Path.Combine(_directory, SafeName(name));
        lock (_sync)
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