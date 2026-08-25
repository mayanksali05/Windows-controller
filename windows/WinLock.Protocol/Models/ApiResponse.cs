using System.Text.Json.Serialization;

namespace WinLock.Protocol.Models;

/// <summary>Standard response envelope used by every API endpoint.</summary>
public class ApiResponse
{
    public bool Success { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ApiError? Error { get; init; }

    public static ApiResponse SuccessResult(string? message = null) =>
        new() { Success = true, Message = message };

    public static ApiResponse Failure(string code, string message) =>
        new() { Success = false, Error = new ApiError { Code = code, Message = message } };
}

/// <summary>Response envelope carrying typed payload data.</summary>
public sealed class ApiResponse<T> : ApiResponse
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new() { Success = true, Message = message, Data = data };

    public static new ApiResponse<T> Failure(string code, string message) =>
        new() { Success = false, Error = new ApiError { Code = code, Message = message } };
}