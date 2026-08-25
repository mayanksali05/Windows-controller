import Foundation

/// A laptop the user has paired with. Only metadata lives here; keys and the
/// TLS pin live in the Keychain.
struct PairedLaptop: Codable, Identifiable, Hashable {
    var id: String { deviceId }
    let deviceId: String
    var name: String
    var host: String
    var port: Int
    var pairedAt: Date
}