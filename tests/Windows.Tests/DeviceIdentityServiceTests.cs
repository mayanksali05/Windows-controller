using WinLock.Cryptography;
using Xunit;

namespace Windows.Tests;

public sealed class DeviceIdentityServiceTests : IDisposable
{
    private readonly string _directory;
    private readonly DpapiSecureStorage _storage;
    private readonly ISigningService _signing;

    public DeviceIdentityServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "winlock-tests", Guid.NewGuid().ToString("N"));
        _storage = new DpapiSecureStorage(_directory);
        _signing = new Ed25519SigningService();
    }

    [Fact]
    public void CreatesStableIdentity_AcrossInstances()
    {
        var first = new DeviceIdentityService(_storage, _signing);
        var second = new DeviceIdentityService(_storage, _signing);

        Assert.Equal(first.DeviceId, second.DeviceId);
        Assert.Equal(first.PublicKey, second.PublicKey);
        Assert.Equal(32, first.PublicKey.Length);
    }

    [Fact]
    public void Sign_VerifiesWithPublicKey()
    {
        var identity = new DeviceIdentityService(_storage, _signing);
        var message = System.Text.Encoding.UTF8.GetBytes("hello\x1fworld\x1e");

        var signature = identity.Sign(message);

        Assert.True(_signing.Verify(identity.PublicKey, message, signature));
    }

    [Fact]
    public void BlobIsDpapiProtected_NotPlaintext()
    {
        new DeviceIdentityService(_storage, _signing);

        var files = Directory.GetFiles(_directory, "*.dpapi");
        Assert.NotEmpty(files);

        var raw = File.ReadAllText(files[0]);
        Assert.DoesNotContain("deviceId", raw);
        Assert.DoesNotContain("\"Seed\"", raw);
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