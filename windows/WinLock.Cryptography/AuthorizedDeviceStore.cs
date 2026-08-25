using System.Text.Json;

namespace WinLock.Cryptography;

/// <summary>
/// DPAPI-protected store of authorized client public keys and metadata.
/// Adding, listing, and removing are thread-safe and persisted atomically.
/// File access is serialized process-wide so overlapping holders (service +
/// tray) cannot corrupt the blob.
/// </summary>
public sealed class AuthorizedDeviceStore
{
    private const string StoreKeyName = "authorized-devices.v1";
    private static readonly object GlobalSync = new();

    private readonly ISecureStorage _storage;
    private readonly object _instanceSync = new();
    private readonly Dictionary<string, AuthorizedDevice> _devices;

    public AuthorizedDeviceStore(ISecureStorage storage)
    {
        _storage = storage;
        _devices = Load();
    }

    public int Count
    {
        get { lock (_instanceSync) { return _devices.Count; } }
    }

    public bool IsPaired(string deviceId)
    {
        lock (_instanceSync)
        {
            return _devices.ContainsKey(deviceId);
        }
    }

    public AuthorizedDevice? GetByDeviceId(string deviceId)
    {
        lock (_instanceSync)
        {
            return _devices.GetValueOrDefault(deviceId);
        }
    }

    public IReadOnlyList<AuthorizedDevice> GetAll()
    {
        lock (_instanceSync)
        {
            return _devices.Values.OrderBy(d => d.PairedAtUtc).ToList();
        }
    }

    public bool TryAdd(AuthorizedDevice device)
    {
        lock (_instanceSync)
        {
            if (_devices.ContainsKey(device.DeviceId))
            {
                return false;
            }

            _devices[device.DeviceId] = device;
            Persist();
            return true;
        }
    }

    public bool TryRemove(string deviceId)
    {
        lock (_instanceSync)
        {
            if (!_devices.Remove(deviceId))
            {
                return false;
            }

            Persist();
            return true;
        }
    }

    private Dictionary<string, AuthorizedDevice> Load()
    {
        lock (GlobalSync)
        {
            var data = _storage.Load(StoreKeyName);
            if (data is null)
            {
                return new Dictionary<string, AuthorizedDevice>();
            }

            try
            {
                var list = JsonSerializer.Deserialize<List<AuthorizedDevice>>(data) ?? new List<AuthorizedDevice>();
                return list.ToDictionary(d => d.DeviceId);
            }
            catch (JsonException)
            {
                return new Dictionary<string, AuthorizedDevice>();
            }
        }
    }

    private void Persist()
    {
        lock (GlobalSync)
        {
            var list = _devices.Values.ToList();
            _storage.Save(StoreKeyName, JsonSerializer.SerializeToUtf8Bytes(list));
        }
    }
}