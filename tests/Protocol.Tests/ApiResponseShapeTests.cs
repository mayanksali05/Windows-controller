using System.Text.Json;
using WinLock.Protocol;
using WinLock.Protocol.Models;
using Xunit;

namespace Protocol.Tests;

public sealed class ApiResponseShapeTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Failure_Serializes_StructuredEnvelope()
    {
        var response = ApiResponse.Failure(ErrorCodes.AuthFailed, "Authentication failed");

        using var doc = JsonSerializer.SerializeToDocument(response, WebJson);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(ErrorCodes.AuthFailed, root.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal("Authentication failed", root.GetProperty("error").GetProperty("message").GetString());
        Assert.False(root.TryGetProperty("message", out _), "Failure responses must not carry a message");
        Assert.False(root.TryGetProperty("data", out _), "Failure responses must not carry data");
    }

    [Fact]
    public void Success_Serializes_MessageAndData()
    {
        var response = ApiResponse<StatusDto>.Ok(new StatusDto
        {
            IsLocked = false,
            BatteryPercent = 74,
            ServiceVersion = "0.1.0",
            Environment = "Development",
            LockAvailable = true,
            Proximity = "UNKNOWN",
            Security = "NOT_PAIRED",
        }, "OK");

        using var doc = JsonSerializer.SerializeToDocument(response, WebJson);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal("OK", root.GetProperty("message").GetString());
        Assert.False(root.TryGetProperty("error", out _));
        Assert.Equal(74, root.GetProperty("data").GetProperty("batteryPercent").GetInt32());
        Assert.Equal("UNKNOWN", root.GetProperty("data").GetProperty("proximity").GetString());
    }

    [Fact]
    public void SuccessResult_WithoutMessage_OmitsMessage()
    {
        var response = ApiResponse.SuccessResult();

        using var doc = JsonSerializer.SerializeToDocument(response, WebJson);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.False(root.TryGetProperty("message", out _));
    }

    [Fact]
    public void RoundTrips_CamelCase()
    {
        var response = ApiResponse<StatusDto>.Ok(new StatusDto { IsLocked = null });

        var json = JsonSerializer.Serialize(response, WebJson);
        var roundTripped = JsonSerializer.Deserialize<ApiResponse<StatusDto>>(json, WebJson);

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped.Success);
        Assert.Null(roundTripped.Data!.IsLocked);
    }
}