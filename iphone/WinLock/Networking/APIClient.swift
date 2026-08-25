import Foundation

/// Typed client for the WinLock service. Handles Face-ID-gated
/// challenge-response authentication and transparently re-authenticates when
/// the session token expires.
final class APIClient {
    private let baseURL: URL
    private let session: URLSession
    private let identity: DeviceKeys
    private let deviceId: String
    private let decoder: JSONDecoder
    private var sessionToken: String?
    private var sessionExpires: Date?

    init(baseURL: URL, identity: DeviceKeys, deviceId: String, expectedPin: String?, mode: TrustMode) {
        self.baseURL = baseURL
        self.identity = identity
        self.deviceId = deviceId

        let configuration = URLSessionConfiguration.ephemeral
        configuration.timeoutIntervalForRequest = 10
        configuration.waitsForConnectivity = false
        self.session = URLSession(
            configuration: configuration,
            delegate: ServerTrustPinner(expectedPin: expectedPin, mode: mode),
            delegateQueue: nil)

        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        self.decoder = decoder
    }

    // MARK: - Operations

    func getStatus() async throws -> LaptopStatus {
        let response: ApiResponse<LaptopStatus> = try await send("/api/v1/status", method: "GET")
        return try unwrap(response)
    }

    func lock() async throws {
        let response: ApiResponse<EmptyData> = try await send(
            "/api/v1/lock", method: "POST", body: ["device_id": deviceId])
        try requireSuccess(response)
    }

    /// Used during pairing: confirms the scanned payload without an existing session.
    func pairConfirm(payload: PairingSessionPayload) async throws {
        let input = ProtocolStrings.pairingSigningInput(deviceId: deviceId, nonce: payload.pairingNonce)
        let signature = try identity.sign(input)
        let body: [String: String] = [
            "device_id": payload.deviceId,
            "client_device_id": deviceId,
            "client_public_key": identity.publicKeyBase64URL,
            "pairing_token": payload.pairingToken,
            "signature": Base64URL.encode(signature)
        ]
        let response: ApiResponse<EmptyData> = try await send(
            "/api/v1/pair/confirm", method: "POST", body: body, authenticated: false)
        try requireSuccess(response)
    }

    func listDevices() async throws -> [AuthorizedDevice] {
        let response: ApiResponse<[AuthorizedDevice]> = try await send("/api/v1/pair/devices", method: "GET")
        return try unwrap(response)
    }

    func unpair(deviceId: String) async throws {
        let response: ApiResponse<EmptyData> = try await send(
            "/api/v1/unpair", method: "POST", body: ["device_id": deviceId])
        try requireSuccess(response)
    }

    // MARK: - Request plumbing

    private func send<T: Decodable>(
        _ path: String,
        method: String,
        body: [String: String]? = nil,
        authenticated: Bool = true
    ) async throws -> ApiResponse<T> {
        if authenticated && !hasValidSession() {
            try await authenticate()
        }

        var request = URLRequest(url: URL(string: path, relativeTo: baseURL)!)
        request.httpMethod = method
        request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        if authenticated, let sessionToken {
            request.setValue("Bearer \(sessionToken)", forHTTPHeaderField: "Authorization")
        }
        if let body {
            request.httpBody = try JSONSerialization.data(withJSONObject: body)
        }

        let (data, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse else {
            throw APIError.invalidResponse
        }

        if http.statusCode == 401, authenticated {
            sessionToken = nil
            try await authenticate()
            return try await send(path, method: method, body: body, authenticated: authenticated)
        }

        guard let decoded = try? decoder.decode(ApiResponse<T>.self, from: data) else {
            throw APIError.invalidResponse
        }
        return decoded
    }

    private func authenticate() async throws {
        // Face ID gates the signing of the challenge.
        try await FaceIDAuthenticator.evaluate(reason: "Authenticate to WinLock")

        let challengeResponse: ApiResponse<AuthChallenge> = try await send(
            "/api/v1/auth/challenge", method: "POST",
            body: ["device_id": deviceId], authenticated: false)
        let challenge = try unwrap(challengeResponse)

        let timestamp = ISO8601DateFormatter().string(from: Date())
        let input = ProtocolStrings.authenticationSigningInput(
            deviceId: deviceId,
            challenge: challenge.challenge,
            timestamp: timestamp,
            endpoint: ProtocolStrings.challengeVerifyEndpoint)
        let signature = try identity.sign(input)

        let verifyBody: [String: String] = [
            "client_device_id": deviceId,
            "challenge_id": challenge.challengeId,
            "timestamp": timestamp,
            "signature": Base64URL.encode(signature)
        ]
        let verifyResponse: ApiResponse<AuthVerifyResponse> = try await send(
            "/api/v1/auth/verify", method: "POST",
            body: verifyBody, authenticated: false)
        let session = try unwrap(verifyResponse)

        sessionToken = session.sessionToken
        sessionExpires = ISO8601DateFormatter().date(from: session.sessionExpires)
    }

    private func hasValidSession() -> Bool {
        guard let sessionToken, let sessionExpires else { return false }
        return Date() < sessionExpires
    }

    private func unwrap<T>(_ response: ApiResponse<T>) throws -> T {
        guard response.success else {
            throw APIError.server(
                code: response.error?.code ?? "UNKNOWN",
                message: response.error?.message ?? "Unknown error")
        }
        guard let data = response.data else { throw APIError.invalidResponse }
        return data
    }

    private func requireSuccess<T>(_ response: ApiResponse<T>) throws {
        guard response.success else {
            throw APIError.server(
                code: response.error?.code ?? "UNKNOWN",
                message: response.error?.message ?? "Unknown error")
        }
    }
}