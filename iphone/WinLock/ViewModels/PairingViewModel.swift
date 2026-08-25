import Foundation

/// Drives the pairing flow: verifies the scanned QR payload, proves the
/// Windows identity, confirms pairing with a signed message, and stores the
/// paired laptop plus the TLS pin.
@MainActor
final class PairingViewModel: ObservableObject {
    enum Stage {
        case scanning
        case verifying
        case confirming
        case done
    }

    @Published var stage: Stage = .scanning
    @Published var errorMessage: String?
    @Published var pairedLaptop: PairedLaptop?

    private let identity: DeviceKeys
    private let deviceId: String
    private let pairedStore: PairedLaptopStore
    private let log: LogStore
    private let container: ServiceContainer

    init(container: ServiceContainer) {
        self.container = container
        self.identity = container.identity
        self.deviceId = container.deviceId
        self.pairedStore = container.pairedLaptops
        self.log = container.logStore
    }

    func processPayload(_ raw: String, host: String, port: Int) async {
        guard let data = raw.data(using: .utf8) else {
            errorMessage = APIError.invalidPairingPayload.localizedDescription
            return
        }

        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        guard let payload = try? decoder.decode(PairingSessionPayload.self, from: data) else {
            errorMessage = APIError.invalidPairingPayload.localizedDescription
            return
        }

        stage = .verifying
        errorMessage = nil

        // 1. Verify the Windows identity: the payload's signature over
        //    canonical(device_id, nonce) must validate against the Windows
        //    public key. This proves the payload came from the key holder.
        guard let windowsPublicKey = Base64URL.decode(payload.windowsPublicKey),
              let signature = Base64URL.decode(payload.signature) else {
            errorMessage = APIError.invalidPairingPayload.localizedDescription
            stage = .scanning
            return
        }
        let verifyInput = ProtocolStrings.pairingSigningInput(deviceId: payload.deviceId, nonce: payload.pairingNonce)
        guard DeviceKeys.verify(publicKey: windowsPublicKey, signature: signature, message: verifyInput) else {
            log.add(kind: "PAIRING_FAILED", "Pairing signature verification failed")
            errorMessage = "Pairing payload signature is invalid"
            stage = .scanning
            return
        }

        // 2. The TLS pin must travel with the payload so the confirm request is
        //    pinned from the very first connection.
        guard let pin = payload.tlsPin, !pin.isEmpty else {
            errorMessage = "Pairing payload is missing the TLS pin"
            stage = .scanning
            return
        }

        // 3. Confirm pairing by signing the pairing nonce with the iPhone key.
        stage = .confirming
        let client = container.pairingClient(host: host, port: port, pin: pin)
        do {
            try await client.pairConfirm(payload: payload)
        } catch {
            log.add(kind: "PAIRING_FAILED", "Pairing confirm failed: \(error.localizedDescription)")
            errorMessage = error.localizedDescription
            stage = .scanning
            return
        }

        // 4. Persist the pairing (metadata in UserDefaults, keys/pin in Keychain).
        let laptop = PairedLaptop(
            deviceId: payload.deviceId,
            name: payload.deviceId,
            host: host,
            port: port,
            pairedAt: Date())
        pairedStore.add(laptop)
        try? KeychainStore.save(windowsPublicKey, key: KeychainKeys.windowsPublicKey(for: payload.deviceId))
        try? KeychainStore.save(Data(pin.utf8), key: KeychainKeys.tlsPin(for: payload.deviceId))

        log.add(kind: "PAIRING_COMPLETED", "Paired with \(laptop.name)")
        pairedLaptop = laptop
        stage = .done
        container.updateAdvertising()
    }
}