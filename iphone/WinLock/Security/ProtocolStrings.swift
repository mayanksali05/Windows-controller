import Foundation

/// Deterministic byte sequences that are signed with Ed25519. Must produce
/// byte-for-byte identical input to the Windows `ProtocolStrings` helpers.
enum ProtocolStrings {
    static let challengeVerifyEndpoint = "/api/v1/auth/verify"
    static let pairingConfirmEndpoint = "/api/v1/pair/confirm"

    static func pairingSigningInput(deviceId: String, nonce: String) -> Data {
        canonical([deviceId, nonce])
    }

    static func authenticationSigningInput(deviceId: String, challenge: String, timestamp: String, endpoint: String) -> Data {
        canonical([deviceId, challenge, timestamp, endpoint])
    }

    /// Fields joined with 0x1F (unit separator), single trailing 0x1E
    /// (record separator), matching the Windows canonical string.
    private static func canonical(_ parts: [String]) -> Data {
        var data = Data()
        for (index, part) in parts.enumerated() {
            data.append(Data(part.utf8))
            data.append(index == parts.count - 1 ? 0x1E : 0x1F)
        }
        return data
    }
}