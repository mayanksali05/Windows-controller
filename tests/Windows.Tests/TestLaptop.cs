using System.Net.Http.Json;
using WinLock.Cryptography;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using Xunit;

namespace Windows.Tests;

/// <summary>
/// Shared harness: authenticates to the test host as "the laptop itself" using
/// the Windows identity key, through the same challenge-response protocol the
/// iPhone (and tray) use. The identity lives in the same DPAPI storage the test
/// host reads, so signatures verify.
/// </summary>
public static class TestLaptop
{
    private static readonly Lazy<DeviceIdentityService> Identity = new(() =>
        new DeviceIdentityService(new DpapiSecureStorage(StorageDirectory), new Ed25519SigningService()));

    public static string StorageDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLock", "storage");

    public static DeviceIdentityService Device => Identity.Value;

    public static async Task<string> AuthenticateAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        var identity = Device;

        var challenge = await client.PostAsJsonAsync("/api/v1/auth/challenge",
            new AuthChallengeRequestDto { DeviceId = identity.DeviceId }, cancellationToken);
        challenge.EnsureSuccessStatusCode();
        var challengeBody = await challenge.Content.ReadFromJsonAsync<ApiResponse<AuthChallengeDto>>(cancellationToken);
        Assert.NotNull(challengeBody?.Data);

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var input = ProtocolStrings.AuthenticationSigningInput(
            identity.DeviceId, challengeBody!.Data!.Challenge, timestamp, ProtocolStrings.ChallengeVerifyEndpoint);
        var signature = identity.Sign(input);

        var verify = await client.PostAsJsonAsync("/api/v1/auth/verify", new AuthVerifyRequestDto
        {
            ClientDeviceId = identity.DeviceId,
            ChallengeId = challengeBody.Data.ChallengeId,
            Timestamp = timestamp,
            Signature = Base64Url.Encode(signature),
        }, cancellationToken);
        verify.EnsureSuccessStatusCode();

        var verifyBody = await verify.Content.ReadFromJsonAsync<ApiResponse<AuthVerifyResponseDto>>(cancellationToken);
        Assert.NotNull(verifyBody?.Data);
        return verifyBody!.Data!.SessionToken;
    }
}