using System.Security.Cryptography;
using System.Text.Json;
using WinLock.Protocol;

namespace WinLock.Cryptography;

/// <summary>
/// The Windows laptop's persistent identity: an Ed25519 key pair generated on
/// first run and stored DPAPI-protected. The device ID is derived from the
/// public key, so identity is bound to the key. Shared by the service (as
/// server identity) and the tray application (which authenticates to the
/// service as "the laptop itself" using this same key).
/// </summary>
public sealed class DeviceIdentityService
{
    private const string IdentityKeyName = "device-identity.v1";
    private static readonly object CreationSync = new();

    private readonly string _deviceId;
    private readonly byte[] _privateKeySeed;

    public DeviceIdentityService(ISecureStorage storage, ISigningService signing)
    {
        (_deviceId, _privateKeySeed) = LoadOrCreate(storage);
    }

    public string DeviceId => _deviceId;

    public byte[] PublicKey => Ed25519.DerivePublicKey(_privateKeySeed);

    public string PublicKeyBase64Url => Base64Url.Encode(PublicKey);

    public byte[] Sign(byte[] message) => Ed25519.Sign(_privateKeySeed, message);

    private static (string deviceId, byte[] seed) LoadOrCreate(ISecureStorage storage)
    {
        var existing = LoadValid(storage);
        if (existing is not null)
        {
            return existing.Value;
        }

        lock (CreationSync)
        {
            existing = LoadValid(storage);
            if (existing is not null)
            {
                return existing.Value;
            }

            var seed = Ed25519.GeneratePrivateKeySeed();
            var publicKey = Ed25519.DerivePublicKey(seed);
            var deviceId = Convert.ToHexString(SHA256.HashData(publicKey))[..16];

            storage.Save(IdentityKeyName,
                JsonSerializer.SerializeToUtf8Bytes(new StoredIdentity { DeviceId = deviceId, Seed = seed }));

            return (deviceId, seed);
        }
    }

    private static (string deviceId, byte[] seed)? LoadValid(ISecureStorage storage)
    {
        var existing = storage.Load(IdentityKeyName);
        if (existing is null)
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredIdentity>(existing);
            if (stored is not null && stored.Seed.Length == Ed25519.PublicKeySize)
            {
                return (stored.DeviceId, stored.Seed);
            }
        }
        catch (JsonException)
        {
            // Fall through and regenerate a fresh identity.
        }

        return null;
    }

    private sealed class StoredIdentity
    {
        public string DeviceId { get; set; } = string.Empty;
        public byte[] Seed { get; set; } = Array.Empty<byte>();
    }
}