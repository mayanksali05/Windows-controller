import CryptoKit
import Foundation

/// Central dependency container for the app. Holds the iPhone identity
/// (an Ed25519 key pair stored in the Keychain), the paired-laptop metadata,
/// discovery, and a local security event log.
@MainActor
final class ServiceContainer: ObservableObject {
    let identity: DeviceKeys
    let deviceId: String
    let pairedLaptops: PairedLaptopStore
    let discovery: BonjourDiscovery
    let logStore: LogStore

    private var _proximityAdvertiser: ProximityAdvertiser?

    init() {
        // Load or create the iPhone identity in the Keychain.
        let identity: DeviceKeys
        if let data = KeychainStore.load(key: KeychainKeys.identityPrivateKey),
           let keys = try? DeviceKeys(privateKeyData: data) {
            identity = keys
        } else {
            let keys = DeviceKeys()
            try? KeychainStore.save(keys.privateKeyData, key: KeychainKeys.identityPrivateKey)
            identity = keys
        }
        self.identity = identity

        // The device id is derived from the public key, so it is stable and
        // bound to the key (mirrors the Windows laptop identity scheme).
        let digest = SHA256.hash(data: identity.publicKeyData)
        self.deviceId = digest.prefix(8).map { String(format: "%02X", $0) }.joined()

        self.pairedLaptops = PairedLaptopStore()
        self.discovery = BonjourDiscovery()
        self.logStore = LogStore()
    }

    /// Builds an authenticated client for a paired laptop using its pinned TLS
    /// certificate and the stored Windows public key.
    func client(for laptop: PairedLaptop) -> APIClient {
        let pinData = KeychainStore.load(key: KeychainKeys.tlsPin(for: laptop.deviceId))
        let pin = pinData.flatMap { String(data: $0, encoding: .utf8) }
        return APIClient(
            baseURL: URL(string: "https://\(laptop.host):\(laptop.port)")!,
            identity: identity,
            deviceId: deviceId,
            expectedPin: pin,
            mode: .development)
    }

    /// Builds an unauthenticated client used only for pairing (confirm), where
    /// the TLS pin comes from the QR payload.
    func pairingClient(host: String, port: Int, pin: String) -> APIClient {
        APIClient(
            baseURL: URL(string: "https://\(host):\(port)")!,
            identity: identity,
            deviceId: deviceId,
            expectedPin: pin,
            mode: .development)
    }

    /// Starts/stops the BLE proximity advertisement depending on whether any
    /// laptop is paired. BLE is a proximity signal only.
    func updateAdvertising() {
        if pairedLaptops.laptops.isEmpty {
            _proximityAdvertiser?.stop()
            _proximityAdvertiser = nil
        } else {
            if _proximityAdvertiser == nil {
                _proximityAdvertiser = ProximityAdvertiser(deviceId: deviceId)
            }
            _proximityAdvertiser?.start()
        }
    }
}