using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using WinLock.Cryptography;
using WinLock.Protocol;
using WinLock.Protocol.Models;

namespace WinLock.Tray;

/// <summary>
/// Talks to the WinLock service over loopback. Authenticates as "the laptop
/// itself" using the shared Windows identity key through the same
/// challenge-response protocol the iPhone uses. TLS is pinned to the
/// CN=WinLock-Development certificate thumbprint in development.
/// </summary>
public sealed class ServiceClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly DeviceIdentityService _identity;
    private string? _sessionToken;
    private DateTimeOffset _sessionExpires;

    public ServiceClient(TrayOptions options, DeviceIdentityService identity)
    {
        _identity = identity;
        _baseUrl = options.UseHttps ? $"https://localhost:{options.Port}" : $"http://localhost:{options.Port}";

        var handler = new HttpClientHandler();
        if (options.UseHttps)
        {
            var expected = CertificateLoader.LoadDevelopmentCertificate()
                ?? throw new InvalidOperationException(
                    "Development certificate (CN=WinLock-Development) not found. Run scripts/setup-windows.ps1 first.");

            var expectedThumbprint = expected.GetCertHashString();
            handler.ServerCertificateCustomValidationCallback = (_, serverCertificate, _, _) =>
                serverCertificate is not null &&
                string.Equals(serverCertificate.GetCertHashString(), expectedThumbprint, StringComparison.OrdinalIgnoreCase);
        }

        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    public async Task<StatusDto?> GetStatusAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(
            new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/status"), cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<StatusDto>>(cancellationToken);
        return body?.Data;
    }

    public async Task<(bool Success, string Message)> LockAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/lock")
        {
            Content = JsonContent.Create(new LockRequest()),
        };
        using var response = await SendAuthedAsync(request, cancellationToken);
        if (response is null)
        {
            return (false, "Service unreachable");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken);
        return (response.IsSuccessStatusCode,
            body?.Message ?? body?.Error?.Message ?? response.StatusCode.ToString());
    }

    /// <summary>Creates a pairing session and returns the full QR payload (authenticated).</summary>
    public async Task<PairingSessionPayloadDto?> CreatePairingSessionAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(
            new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/pair/session"), cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<PairingSessionPayloadDto>>(cancellationToken);
        return body?.Data;
    }

    /// <summary>Lists paired devices (authenticated).</summary>
    public async Task<IReadOnlyList<AuthorizedDeviceDto>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        using var response = await SendAuthedAsync(
            new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/pair/devices"), cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return Array.Empty<AuthorizedDeviceDto>();
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AuthorizedDeviceDto>>>(cancellationToken);
        return (IReadOnlyList<AuthorizedDeviceDto>?)body?.Data ?? Array.Empty<AuthorizedDeviceDto>();
    }

    /// <summary>Removes a paired device (authenticated).</summary>
    public async Task<(bool Success, string Message)> UnpairAsync(string deviceId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/unpair")
        {
            Content = JsonContent.Create(new UnpairRequestDto { DeviceId = deviceId }),
        };
        using var response = await SendAuthedAsync(request, cancellationToken);
        if (response is null)
        {
            return (false, "Service unreachable");
        }

        var body = await response.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken);
        return (response.IsSuccessStatusCode,
            body?.Message ?? body?.Error?.Message ?? response.StatusCode.ToString());
    }

    private async Task<HttpResponseMessage?> SendAuthedAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            if (!await EnsureAuthenticatedAsync(cancellationToken))
            {
                return null;
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _sessionToken);
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<bool> EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_sessionToken is not null && _sessionExpires > DateTimeOffset.UtcNow)
        {
            return true;
        }

        var challengeResponse = await _http.PostAsJsonAsync(
            $"{_baseUrl}/api/v1/auth/challenge",
            new AuthChallengeRequestDto { DeviceId = _identity.DeviceId }, cancellationToken);
        if (!challengeResponse.IsSuccessStatusCode)
        {
            return false;
        }

        var challengeBody = await challengeResponse.Content
            .ReadFromJsonAsync<ApiResponse<AuthChallengeDto>>(cancellationToken);
        if (challengeBody?.Data is null)
        {
            return false;
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var input = ProtocolStrings.AuthenticationSigningInput(
            _identity.DeviceId, challengeBody.Data.Challenge, timestamp, ProtocolStrings.ChallengeVerifyEndpoint);
        var signature = _identity.Sign(input);

        var verifyResponse = await _http.PostAsJsonAsync(
            $"{_baseUrl}/api/v1/auth/verify",
            new AuthVerifyRequestDto
            {
                ClientDeviceId = _identity.DeviceId,
                ChallengeId = challengeBody.Data.ChallengeId,
                Timestamp = timestamp,
                Signature = Base64Url.Encode(signature),
            }, cancellationToken);

        if (!verifyResponse.IsSuccessStatusCode)
        {
            return false;
        }

        var verifyBody = await verifyResponse.Content
            .ReadFromJsonAsync<ApiResponse<AuthVerifyResponseDto>>(cancellationToken);
        if (verifyBody?.Data is null)
        {
            return false;
        }

        _sessionToken = verifyBody.Data.SessionToken;
        _sessionExpires = DateTimeOffset.TryParse(verifyBody.Data.SessionExpires, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var expires)
            ? expires
            : DateTimeOffset.UtcNow.AddMinutes(5);

        return true;
    }

    public void Dispose() => _http.Dispose();
}