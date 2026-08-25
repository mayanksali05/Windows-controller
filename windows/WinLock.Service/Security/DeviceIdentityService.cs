using System.Security.Cryptography;
using System.Text.Json;
using WinLock.Protocol;

namespace WinLock.Service.Security;

/// <summary>
/// The Windows laptop's persistent identity: an Ed25519 key pair generated on
/// first run and stored DPAPI-protected. The device ID is derived from the
/// public key, so identity is bound to the key. Signs pairing material so the
/// iPhone can verify the server's identity.
/// </summary>
public sealed class DeviceIdentityService
{
    private const string IdentityKeyName = "device-identity.v1";

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
        var existing = storage.Load(IdentityKeyName);
        if (existing is not null)
        {
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
        }

        var seed = Ed25519.GeneratePrivateKeySeed();
        var publicKey = Ed25519.DerivePublicKey(seed);
        var deviceId = Convert.ToHexString(SHA256.HashData(publicKey))[..16];

        storage.Save(IdentityKeyName,
            JsonSerializer.SerializeToUtf8Bytes(new StoredIdentity { DeviceId = deviceId, Seed = seed }));

        return (deviceId, seed);
    }

    private sealed class StoredIdentity
    {
        public string DeviceId { get; set; } = string.Empty;
        public byte[] Seed { get; set; } = Array.Empty<byte>();
    }
}