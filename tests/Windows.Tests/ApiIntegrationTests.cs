using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WinLock.Protocol.Models;
using WinLock.Protocol;
using WinLock.Service;
using WinLock.Service.Authentication;
using WinLock.Service.Bluetooth;
using WinLock.Service.Locking;
using Xunit;

namespace Windows.Tests;

public sealed class FakeLockService : IWindowsLockService
{
    public bool CanLock => true;
    public Task LockAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class FakeProximityScanner : IBluetoothProximityScanner
{
    public ProximitySnapshot Current { get; private set; } = ProximitySnapshot.Unknown();
    public event EventHandler<ProximitySnapshot>? SnapshotChanged;

    public void SetMonitoredDevices(IReadOnlyCollection<string> deviceIds) { }

    public void UpdateStale() { }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void SetState(ProximitySnapshot snapshot)
    {
        Current = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }
}

/// <summary>
/// Stands in for the credential-provider unlock path: records what it was
/// asked to do and returns a configurable outcome.
/// </summary>
public sealed class FakeUnlockProvider : IWindowsAuthenticationProvider
{
    public bool IsAvailable => true;
    public UnlockResult NextResult { get; set; } = new(UnlockOutcome.Unlocked, "Laptop unlocked");
    public string? LastPassword { get; private set; }
    public string? LastDeviceId { get; private set; }
    public int Calls { get; private set; }

    public Task<UnlockResult> UnlockAsync(UnlockRequest request, CancellationToken cancellationToken)
    {
        Calls++;
        LastPassword = request.Password;
        LastDeviceId = request.DeviceId;
        return Task.FromResult(NextResult);
    }
}

/// <summary>
/// Hosts the real WinLock service over an in-memory test server with the lock
/// service and BLE scanner replaced by fakes (so tests never lock the
/// workstation or start a real Bluetooth watcher).
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:UseHttps"] = "false",
                ["Server:Environment"] = "Development",
                ["Server:Port"] = "0",
                ["Security:AutomaticLockEnabled"] = "false",
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IWindowsLockService, FakeLockService>();
            services.AddSingleton<IBluetoothProximityScanner, FakeProximityScanner>();
            services.AddSingleton<FakeUnlockProvider>();
            services.AddSingleton<IWindowsAuthenticationProvider>(sp => sp.GetRequiredService<FakeUnlockProvider>());
        });
    }
}

public sealed class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;
    private readonly FakeUnlockProvider _unlock;

    public ApiIntegrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
        _unlock = factory.Services.GetRequiredService<FakeUnlockProvider>();
    }

    private void SetBearer(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> AuthenticateLaptopAsync() => await TestLaptop.AuthenticateAsync(_client);

    [Fact]
    public async Task Unauthenticated_Status_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Lock_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/lock", new LockRequest { DeviceId = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidToken_Returns401()
    {
        SetBearer("not-a-session-token");
        var response = await _client.GetAsync("/api/v1/status");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_Status_ReturnsStructuredSuccess()
    {
        SetBearer(await AuthenticateLaptopAsync());
        var response = await _client.GetAsync("/api/v1/status");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<StatusDto>>();

        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.False(string.IsNullOrEmpty(body.Data.ServiceVersion));
    }

    [Fact]
    public async Task ValidToken_Lock_ReturnsSuccess()
    {
        SetBearer(await AuthenticateLaptopAsync());
        var response = await _client.PostAsJsonAsync("/api/v1/lock", new LockRequest());

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();

        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("Laptop locked successfully", body.Message);
    }

    [Fact]
    public async Task MalformedLockBody_ReturnsBadRequest()
    {
        SetBearer(await AuthenticateLaptopAsync());
        var content = new StringContent("{ this is not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/v1/lock", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Proximity_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/proximity");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidToken_Proximity_ReturnsStructuredState()
    {
        SetBearer(await AuthenticateLaptopAsync());
        var response = await _client.GetAsync("/api/v1/proximity");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProximityDto>>();

        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.Equal("UNKNOWN", body.Data!.State);
    }

    [Fact]
    public async Task Status_IncludesProximityState()
    {
        SetBearer(await AuthenticateLaptopAsync());
        var response = await _client.GetAsync("/api/v1/status");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<StatusDto>>();
        Assert.Equal("UNKNOWN", body!.Data!.Proximity);
    }

    [Fact]
    public async Task Settings_ReturnsStructuredPolicy()
    {
        SetBearer(await AuthenticateLaptopAsync());
        var response = await _client.GetAsync("/api/v1/settings");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<SettingsDto>>();

        Assert.NotNull(body);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.False(body.Data.AutomaticLockEnabled); // disabled by test config
        Assert.True(body.Data.ProximityEnabled);
    }

    [Fact]
    public async Task Settings_WithoutAuth_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/settings");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // The unlock rate limit is 5 per minute for the whole host, and this
    // class shares one host, so keep the number of /unlock calls below that.

    [Fact]
    public async Task Unauthenticated_Unlock_Returns401_AndNeverReachesProvider()
    {
        var calls = _unlock.Calls;
        var response = await _client.PostAsJsonAsync("/api/v1/unlock",
            new UnlockRequestDto { DeviceId = "x", Password = "secret" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(calls, _unlock.Calls);
    }

    [Fact]
    public async Task Unlock_ForwardsPasswordToProvider_AndReturnsSuccess()
    {
        SetBearer(await AuthenticateLaptopAsync());
        _unlock.NextResult = new UnlockResult(UnlockOutcome.Unlocked, "Laptop unlocked");

        var response = await _client.PostAsJsonAsync("/api/v1/unlock",
            new UnlockRequestDto { DeviceId = "phone", Password = "correct horse" });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.True(body!.Success);
        Assert.Equal("Laptop unlocked", body.Message);
        Assert.Equal("correct horse", _unlock.LastPassword);
        // The audited device id is the authenticated one, not the body field.
        Assert.Equal(TestLaptop.Device.DeviceId, _unlock.LastDeviceId);
    }

    [Fact]
    public async Task Unlock_WithoutPassword_ReturnsBadRequest()
    {
        SetBearer(await AuthenticateLaptopAsync());
        var response = await _client.PostAsJsonAsync("/api/v1/unlock", new UnlockRequestDto { DeviceId = "phone" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unlock_RejectedPassword_ReturnsStructuredError()
    {
        SetBearer(await AuthenticateLaptopAsync());
        _unlock.NextResult = new UnlockResult(UnlockOutcome.PasswordRejected, "Windows rejected the password.");

        var response = await _client.PostAsJsonAsync("/api/v1/unlock",
            new UnlockRequestDto { Password = "wrong" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.False(body!.Success);
        Assert.Equal(ErrorCodes.UnlockPasswordRejected, body.Error!.Code);
        _unlock.NextResult = new UnlockResult(UnlockOutcome.Unlocked, "Laptop unlocked");
    }
}
