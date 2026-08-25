import Foundation

/// The full pairing payload shown as a QR code on the Windows laptop and
/// returned by `POST /api/v1/pair/session`. Decodes from snake_case JSON.
struct PairingSessionPayload: Decodable {
    let version: Int
    let deviceId: String
    let windowsPublicKey: String
    let pairingNonce: String
    let pairingToken: String
    let expiresAt: String
    let signature: String
    let tlsPin: String?
}