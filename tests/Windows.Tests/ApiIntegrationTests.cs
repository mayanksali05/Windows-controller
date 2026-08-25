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
using WinLock.Service;
using WinLock.Service.Locking;
using Xunit;

namespace Windows.Tests;

public sealed class FakeLockService : IWindowsLockService
{
    public bool CanLock => true;
    public Task LockAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Hosts the real WinLock service over an in-memory test server with the lock
/// service replaced by a fake (so tests never lock the workstation).
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
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IWindowsLockService, FakeLockService>();
        });
    }
}

public sealed class ApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
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
}