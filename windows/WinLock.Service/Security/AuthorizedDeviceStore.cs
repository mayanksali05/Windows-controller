using System.Text.Json;
using WinLock.Protocol;

namespace WinLock.Service.Security;

/// <summary>
/// DPAPI-protected store of authorized iPhone public keys and metadata.
/// Adding, listing, and removing are thread-safe and persisted atomically.
/// </summary>
public sealed class AuthorizedDeviceStore
{
    private const string StoreKeyName = "authorized-devices.v1";

    private readonly ISecureStorage _storage;
    private readonly object _sync = new();
    private readonly Dictionary<string, AuthorizedDevice> _devices;

    public AuthorizedDeviceStore(ISecureStorage storage)
    {
        _storage = storage;
        _devices = Load();
    }

    public int Count
    {
        get { lock (_sync) { return _devices.Count; } }
    }

    public bool IsPaired(string deviceId)
    {
        lock (_sync)
        {
            return _devices.ContainsKey(deviceId);
        }
    }

    public AuthorizedDevice? GetByDeviceId(string deviceId)
    {
        lock (_sync)
        {
            return _devices.GetValueOrDefault(deviceId);
        }
    }

    public IReadOnlyList<AuthorizedDevice> GetAll()
    {
        lock (_sync)
        {
            return _devices.Values.OrderBy(d => d.PairedAtUtc).ToList();
        }
    }

    public bool TryAdd(AuthorizedDevice device)
    {
        lock (_sync)
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
        lock (_sync)
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

    private void Persist()
    {
        var list = _devices.Values.ToList();
        _storage.Save(StoreKeyName, JsonSerializer.SerializeToUtf8Bytes(list));
    }
}