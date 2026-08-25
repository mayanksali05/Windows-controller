using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service;
using WinLock.Service.Security;
using Xunit;

namespace Windows.Tests;

/// <summary>Simulates the iPhone side: an Ed25519 identity and pairing signatures.</summary>
public sealed record TestClientIdentity(string DeviceId, byte[] PrivateSeed, byte[] PublicKey, string PublicKeyBase64Url)
{
    public static TestClientIdentity Create(string deviceId)
    {
        var seed = Ed25519.GeneratePrivateKeySeed();
        var publicKey = Ed25519.DerivePublicKey(seed);
        return new TestClientIdentity(deviceId, seed, publicKey, Base64Url.Encode(publicKey));
    }

    public string Sign(string nonce) =>
        Base64Url.Encode(Ed25519.Sign(PrivateSeed, ProtocolStrings.PairingSigningInput(DeviceId, nonce)));

    public bool Verify(string publicKeyBase64Url, string nonce, string signature) =>
        Ed25519.Verify(
            Base64Url.Decode(publicKeyBase64Url),
            ProtocolStrings.PairingSigningInput(DeviceId, nonce),
            Base64Url.Decode(signature));
}

public sealed class PairingApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public PairingApiIntegrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private void SetBearer(string? token) =>
        _client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

    private async Task<string> GetDevTokenAsync()
    {
        var response = await _client.GetAsync("/api/v1/dev/token");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<DevTokenDto>>();
        Assert.NotNull(body?.Data);
        return body!.Data!.Token;
    }

    private async Task<PairingSessionPayloadDto> CreateSessionAsync(string token)
    {
        SetBearer(token);
        var response = await _client.PostAsync("/api/v1/pair/session", null);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PairingSessionPayloadDto>>();
        Assert.NotNull(body?.Data);
        return body!.Data!;
    }

    [Fact]
    public async Task Pairing_EndToEnd_Works_And_Unpairs()
    {
        var token = await GetDevTokenAsync();
        var payload = await CreateSessionAsync(token);

        // Windows proves identity by signing the nonce over the *server* device id.
        var phone = TestClientIdentity.Create($"phone-{Guid.NewGuid():N}"[..16]);
        Assert.True(Ed25519.Verify(
            Base64Url.Decode(payload.WindowsPublicKey),
            ProtocolStrings.PairingSigningInput(payload.DeviceId, payload.PairingNonce),
            Base64Url.Decode(payload.Signature)));

        // Confirm (anonymous, uses one-time token + signature).
        SetBearer(null);
        var confirm = await _client.PostAsJsonAsync("/api/v1/pair/confirm", new PairingConfirmationDto
        {
            DeviceId = payload.DeviceId,
            ClientDeviceId = phone.DeviceId,
            ClientPublicKey = phone.PublicKeyBase64Url,
            PairingToken = payload.PairingToken,
            Signature = phone.Sign(payload.PairingNonce),
        });
        confirm.EnsureSuccessStatusCode();

        // Device is now authorized.
        SetBearer(token);
        var devices = await _client.GetAsync("/api/v1/pair/devices");
        devices.EnsureSuccessStatusCode();
        var devicesBody = await devices.Content.ReadFromJsonAsync<ApiResponse<List<AuthorizedDeviceDto>>>();
        Assert.Contains(devicesBody!.Data!, d => d.DeviceId == phone.DeviceId);

        // Status reflects paired state.
        var status = await _client.GetAsync("/api/v1/status");
        var statusBody = await status.Content.ReadFromJsonAsync<ApiResponse<StatusDto>>();
        Assert.Equal("PAIRED", statusBody!.Data!.Security);

        // Unpair cleans up.
        var unpair = await _client.PostAsJsonAsync("/api/v1/unpair", new UnpairRequestDto { DeviceId = phone.DeviceId });
        unpair.EnsureSuccessStatusCode();

        var devicesAfter = await _client.GetAsync("/api/v1/pair/devices");
        var devicesAfterBody = await devicesAfter.Content.ReadFromJsonAsync<ApiResponse<List<AuthorizedDeviceDto>>>();
        Assert.DoesNotContain(devicesAfterBody!.Data!, d => d.DeviceId == phone.DeviceId);
    }

    [Fact]
    public async Task Confirm_WithBadSignature_IsRejected()
    {
        var token = await GetDevTokenAsync();
        var payload = await CreateSessionAsync(token);
        var phone = TestClientIdentity.Create($"phone-{Guid.NewGuid():N}"[..16]);

        SetBearer(null);
        var confirm = await _client.PostAsJsonAsync("/api/v1/pair/confirm", new PairingConfirmationDto
        {
            DeviceId = payload.DeviceId,
            ClientDeviceId = phone.DeviceId,
            ClientPublicKey = phone.PublicKeyBase64Url,
            PairingToken = payload.PairingToken,
            Signature = Base64Url.Encode(new byte[Ed25519.SignatureSize]),
        });

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        var body = await confirm.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.Equal(ErrorCodes.PairingInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task Confirm_WithUnknownPublicKey_IsRejected()
    {
        var token = await GetDevTokenAsync();
        var payload = await CreateSessionAsync(token);
        var phone = TestClientIdentity.Create($"phone-{Guid.NewGuid():N}"[..16]);

        SetBearer(null);
        var confirm = await _client.PostAsJsonAsync("/api/v1/pair/confirm", new PairingConfirmationDto
        {
            DeviceId = payload.DeviceId,
            ClientDeviceId = phone.DeviceId,
            ClientPublicKey = Base64Url.Encode(new byte[8]),
            PairingToken = payload.PairingToken,
            Signature = phone.Sign(payload.PairingNonce),
        });

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
    }

    [Fact]
    public async Task Confirm_TokenReplay_IsRejected()
    {
        var token = await GetDevTokenAsync();
        var payload = await CreateSessionAsync(token);
        var phoneA = TestClientIdentity.Create($"phone-{Guid.NewGuid():N}"[..16]);
        var phoneB = TestClientIdentity.Create($"phone-{Guid.NewGuid():N}"[..16]);

        SetBearer(null);
        var first = await _client.PostAsJsonAsync("/api/v1/pair/confirm", new PairingConfirmationDto
        {
            DeviceId = payload.DeviceId,
            ClientDeviceId = phoneA.DeviceId,
            ClientPublicKey = phoneA.PublicKeyBase64Url,
            PairingToken = payload.PairingToken,
            Signature = phoneA.Sign(payload.PairingNonce),
        });
        first.EnsureSuccessStatusCode();

        // Replay the same token with a different client must fail.
        var replay = await _client.PostAsJsonAsync("/api/v1/pair/confirm", new PairingConfirmationDto
        {
            DeviceId = payload.DeviceId,
            ClientDeviceId = phoneB.DeviceId,
            ClientPublicKey = phoneB.PublicKeyBase64Url,
            PairingToken = payload.PairingToken,
            Signature = phoneB.Sign(payload.PairingNonce),
        });

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        var body = await replay.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.Equal(ErrorCodes.PairingInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task PairRequest_ReturnsIdentity_AndVerifiesSignature()
    {
        var token = await GetDevTokenAsync();
        var payload = await CreateSessionAsync(token);

        SetBearer(null);
        var response = await _client.PostAsJsonAsync("/api/v1/pair/request",
            new PairingRequestDto { DeviceId = payload.DeviceId });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PairingInfoDto>>();
        Assert.NotNull(body?.Data);
        Assert.True(body!.Data!.PairingAvailable);
        Assert.Equal(payload.PairingNonce, body.Data.PairingNonce);
        Assert.Equal(payload.WindowsPublicKey, body.Data.WindowsPublicKey);

        Assert.True(Ed25519.Verify(
            Base64Url.Decode(body.Data.WindowsPublicKey),
            ProtocolStrings.PairingSigningInput(payload.DeviceId, payload.PairingNonce),
            Base64Url.Decode(body.Data.Signature!)));
    }

    [Fact]
    public async Task PairRequest_WrongDevice_Returns404()
    {
        SetBearer(null);
        var response = await _client.PostAsJsonAsync("/api/v1/pair/request",
            new PairingRequestDto { DeviceId = "DEADBEEF" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PairSession_WithoutAuth_Returns401()
    {
        SetBearer(null);
        var response = await _client.PostAsync("/api/v1/pair/session", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PairDevices_WithoutAuth_Returns401()
    {
        SetBearer(null);
        var response = await _client.GetAsync("/api/v1/pair/devices");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unpair_WithoutAuth_Returns401()
    {
        SetBearer(null);
        var response = await _client.PostAsJsonAsync("/api/v1/unpair", new UnpairRequestDto { DeviceId = "x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}