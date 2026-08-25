using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using WinLock.Cryptography;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using WinLock.Service;
using Xunit;

namespace Windows.Tests;

public sealed class AuthApiIntegrationTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public AuthApiIntegrationTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    private void SetBearer(string? token) =>
        _client.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);

    private async Task<TestClientIdentity> PairPhoneAsync()
    {
        var laptopToken = await TestLaptop.AuthenticateAsync(_client);
        SetBearer(laptopToken);
        var sessionResponse = await _client.PostAsync("/api/v1/pair/session", null);
        sessionResponse.EnsureSuccessStatusCode();
        var payload = (await sessionResponse.Content.ReadFromJsonAsync<ApiResponse<PairingSessionPayloadDto>>())!.Data!;

        var phone = TestClientIdentity.Create($"phone-{Guid.NewGuid():N}"[..16]);
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
        return phone;
    }

    private async Task<AuthChallengeDto> GetChallengeAsync(string deviceId)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/challenge",
            new AuthChallengeRequestDto { DeviceId = deviceId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AuthChallengeDto>>())!.Data!;
    }

    private static string SignChallenge(TestClientIdentity phone, AuthChallengeDto challenge, string timestamp) =>
        Base64Url.Encode(Ed25519.Sign(phone.PrivateSeed,
            ProtocolStrings.AuthenticationSigningInput(
                phone.DeviceId, challenge.Challenge, timestamp, ProtocolStrings.ChallengeVerifyEndpoint)));

    private async Task<string> VerifyAndGetTokenAsync(TestClientIdentity phone, AuthChallengeDto challenge)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/verify", new AuthVerifyRequestDto
        {
            ClientDeviceId = phone.DeviceId,
            ChallengeId = challenge.ChallengeId,
            Timestamp = timestamp,
            Signature = SignChallenge(phone, challenge, timestamp),
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<AuthVerifyResponseDto>>())!.Data!.SessionToken;
    }

    private async Task UnpairAsync(string deviceId)
    {
        SetBearer(await TestLaptop.AuthenticateAsync(_client));
        var response = await _client.PostAsJsonAsync("/api/v1/unpair", new UnpairRequestDto { DeviceId = deviceId });
        response.EnsureSuccessStatusCode();
        SetBearer(null);
    }

    [Fact]
    public async Task Challenge_ForUnknownDevice_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/challenge",
            new AuthChallengeRequestDto { DeviceId = "DEADBEEF" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.Equal(ErrorCodes.DeviceUnknown, body!.Error!.Code);
    }

    [Fact]
    public async Task FullAuth_AsPairedPhone_GrantsPrivilegedAccess()
    {
        var phone = await PairPhoneAsync();
        var challenge = await GetChallengeAsync(phone.DeviceId);
        var token = await VerifyAndGetTokenAsync(phone, challenge);

        SetBearer(token);
        var status = await _client.GetAsync("/api/v1/status");
        status.EnsureSuccessStatusCode();

        await UnpairAsync(phone.DeviceId);
    }

    [Fact]
    public async Task Verify_WithBadSignature_Fails()
    {
        var phone = await PairPhoneAsync();
        var challenge = await GetChallengeAsync(phone.DeviceId);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/verify", new AuthVerifyRequestDto
        {
            ClientDeviceId = phone.DeviceId,
            ChallengeId = challenge.ChallengeId,
            Timestamp = DateTimeOffset.UtcNow.ToString("O"),
            Signature = Base64Url.Encode(new byte[Ed25519.SignatureSize]),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.Equal(ErrorCodes.AuthFailed, body!.Error!.Code);

        await UnpairAsync(phone.DeviceId);
    }

    [Fact]
    public async Task Verify_ChallengeReplay_IsRejected()
    {
        var phone = await PairPhoneAsync();
        var challenge = await GetChallengeAsync(phone.DeviceId);

        var first = await VerifyAndGetTokenAsync(phone, challenge);
        Assert.False(string.IsNullOrEmpty(first));

        // Replaying the same challenge must fail.
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var replay = await _client.PostAsJsonAsync("/api/v1/auth/verify", new AuthVerifyRequestDto
        {
            ClientDeviceId = phone.DeviceId,
            ChallengeId = challenge.ChallengeId,
            Timestamp = timestamp,
            Signature = SignChallenge(phone, challenge, timestamp),
        });

        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        var body = await replay.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.Equal(ErrorCodes.ChallengeReplayed, body!.Error!.Code);

        await UnpairAsync(phone.DeviceId);
    }

    [Fact]
    public async Task Verify_WithStaleTimestamp_Fails()
    {
        var phone = await PairPhoneAsync();
        var challenge = await GetChallengeAsync(phone.DeviceId);

        // Timestamp older than the configured skew (60s) must be rejected.
        var stale = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/verify", new AuthVerifyRequestDto
        {
            ClientDeviceId = phone.DeviceId,
            ChallengeId = challenge.ChallengeId,
            Timestamp = stale,
            Signature = SignChallenge(phone, challenge, stale),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse>();
        Assert.Equal(ErrorCodes.AuthFailed, body!.Error!.Code);

        await UnpairAsync(phone.DeviceId);
    }

    [Fact]
    public async Task UnpairedDevice_SessionIsRevoked()
    {
        var phone = await PairPhoneAsync();
        var challenge = await GetChallengeAsync(phone.DeviceId);
        var token = await VerifyAndGetTokenAsync(phone, challenge);

        // Unpair the phone; its session must no longer authorize privileged calls.
        await UnpairAsync(phone.DeviceId);

        SetBearer(token);
        var status = await _client.GetAsync("/api/v1/status");
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
    }
}