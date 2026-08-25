import Foundation

/// Body/response of the challenge-response authentication protocol.
struct AuthChallenge: Decodable {
    let challengeId: String
    let challenge: String
    let expiresAt: String
}

struct AuthVerifyResponse: Decodable {
    let sessionToken: String
    let sessionExpires: String
    let proximity: String
}