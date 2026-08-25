import CryptoKit
import Foundation

/// Ed25519 identity using CryptoKit's Curve25519.Signing, interoperable with
/// the Windows BouncyCastle implementation (RFC 8032 raw keys and signatures).
struct DeviceKeys {
    let privateKey: Curve25519.Signing.PrivateKey

    var publicKeyData: Data { privateKey.publicKey.rawRepresentation }
    var privateKeyData: Data { privateKey.rawRepresentation }
    var publicKeyBase64URL: String { Base64URL.encode(publicKeyData) }

    init(privateKeyData: Data) throws {
        privateKey = try Curve25519.Signing.PrivateKey(rawRepresentation: privateKeyData)
    }

    init() {
        privateKey = Curve25519.Signing.PrivateKey()
    }

    func sign(_ data: Data) throws -> Data {
        try privateKey.signature(for: data)
    }

    static func verify(publicKey: Data, signature: Data, message: Data) -> Bool {
        guard let key = try? Curve25519.Signing.PublicKey(rawRepresentation: publicKey) else {
            return false
        }
        return key.isValidSignature(signature, for: message)
    }
}

/// Keychain item names used by the app.
enum KeychainKeys {
    static let identityPrivateKey = "identity.privateKey"
    static func windowsPublicKey(for deviceId: String) -> String { "laptop.\(deviceId).publicKey" }
    static func tlsPin(for deviceId: String) -> String { "laptop.\(deviceId).tlsPin" }
}