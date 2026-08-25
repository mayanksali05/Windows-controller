import Foundation

/// Standard response envelope shared by every WinLock API endpoint.
struct ApiError: Decodable {
    let code: String
    let message: String
}

struct ApiResponse<T: Decodable>: Decodable {
    let success: Bool
    let message: String?
    let error: ApiError?
    let data: T?
}

/// Placeholder used for endpoints that return no data payload.
struct EmptyData: Decodable {}

/// Errors surfaced from the WinLock service API.
enum APIError: LocalizedError {
    case invalidResponse
    case server(code: String, message: String)
    case invalidPairingPayload

    var errorDescription: String? {
        switch self {
        case .invalidResponse: return "Invalid response from the laptop"
        case .server(_, let message): return message
        case .invalidPairingPayload: return "The pairing payload is invalid"
        }
    }
}