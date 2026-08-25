using System.Net.Http.Headers;
using System.Net.Http.Json;
using WinLock.Protocol.Models;

namespace WinLock.Tray;

/// <summary>
/// Talks to the WinLock service over loopback. In development it obtains the
/// runtime dev token from <c>/api/v1/dev/token</c> and pins the TLS certificate
/// to the CN=WinLock-Development certificate thumbprint.
/// </summary>
public sealed class ServiceClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private string? _token;

    public ServiceClient(TrayOptions options)
    {
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

    public bool Initialized => _token is not null;

    /// <summary>Fetches the development bearer token (loopback).</summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/dev/token");
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<DevTokenDto>>(cancellationToken);
            if (body?.Success != true || body.Data is null)
            {
                return false;
            }

            _token = body.Data.Token;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<StatusDto?> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/status");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<StatusDto>>(cancellationToken);
            return body?.Data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<(bool Success, string Message)> LockAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/lock")
            {
                Content = JsonContent.Create(new LockRequest()),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken);
            return (response.IsSuccessStatusCode,
                body?.Message ?? body?.Error?.Message ?? response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>Creates a pairing session and returns the full QR payload (authenticated).</summary>
    public async Task<PairingSessionPayloadDto?> CreatePairingSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/pair/session");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<PairingSessionPayloadDto>>(cancellationToken);
            return body?.Data;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Lists paired devices (authenticated).</summary>
    public async Task<IReadOnlyList<AuthorizedDeviceDto>> ListDevicesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/v1/pair/devices");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<AuthorizedDeviceDto>();
            }

            var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<AuthorizedDeviceDto>>>(cancellationToken);
            return (IReadOnlyList<AuthorizedDeviceDto>?)body?.Data ?? Array.Empty<AuthorizedDeviceDto>();
        }
        catch (Exception)
        {
            return Array.Empty<AuthorizedDeviceDto>();
        }
    }

    /// <summary>Removes a paired device (authenticated).</summary>
    public async Task<(bool Success, string Message)> UnpairAsync(string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/v1/unpair")
            {
                Content = JsonContent.Create(new UnpairRequestDto { DeviceId = deviceId }),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(cancellationToken);
            return (response.IsSuccessStatusCode,
                body?.Message ?? body?.Error?.Message ?? response.StatusCode.ToString());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public void Dispose() => _http.Dispose();
}