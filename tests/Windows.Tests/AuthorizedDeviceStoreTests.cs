using WinLock.Service.Security;
using Xunit;

namespace Windows.Tests;

public sealed class AuthorizedDeviceStoreTests : IDisposable
{
    private readonly string _directory;

    public AuthorizedDeviceStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "winlock-tests", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void Add_List_Get_Work()
    {
        var store = new AuthorizedDeviceStore(new DpapiSecureStorage(_directory));
        var device = new AuthorizedDevice
        {
            DeviceId = "phone-1",
            PublicKeyBase64Url = "AAAA",
            Name = "iPhone",
            PairedAtUtc = DateTimeOffset.UtcNow,
        };

        Assert.True(store.TryAdd(device));
        Assert.True(store.IsPaired("phone-1"));
        Assert.Equal(1, store.Count);
        Assert.Same(device, store.GetByDeviceId("phone-1"));
        Assert.Contains(store.GetAll(), d => d.DeviceId == "phone-1");
    }

    [Fact]
    public void DuplicateAdd_IsRejected()
    {
        var store = new AuthorizedDeviceStore(new DpapiSecureStorage(_directory));
        var device = new AuthorizedDevice { DeviceId = "phone-1", PublicKeyBase64Url = "AAAA" };

        Assert.True(store.TryAdd(device));
        Assert.False(store.TryAdd(device));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Remove_Works()
    {
        var store = new AuthorizedDeviceStore(new DpapiSecureStorage(_directory));
        store.TryAdd(new AuthorizedDevice { DeviceId = "phone-1", PublicKeyBase64Url = "AAAA" });

        Assert.True(store.TryRemove("phone-1"));
        Assert.False(store.TryRemove("phone-1"));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void PersistsAcrossInstances()
    {
        var first = new AuthorizedDeviceStore(new DpapiSecureStorage(_directory));
        first.TryAdd(new AuthorizedDevice
        {
            DeviceId = "phone-1",
            PublicKeyBase64Url = "BBBB",
            Name = "iPhone",
            PairedAtUtc = DateTimeOffset.UtcNow,
        });

        var second = new AuthorizedDeviceStore(new DpapiSecureStorage(_directory));

        Assert.True(second.IsPaired("phone-1"));
        Assert.Equal("BBBB", second.GetByDeviceId("phone-1")!.PublicKeyBase64Url);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}