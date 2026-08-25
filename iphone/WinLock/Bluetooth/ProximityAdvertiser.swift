import CoreBluetooth
import Foundation

/// Advertises the iPhone's per-device BLE service so the Windows laptop can
/// estimate proximity. BLE presence is a proximity signal only and never
/// authorizes anything; privileged operations always require Face ID and the
/// cryptographic challenge-response over Wi-Fi.
final class ProximityAdvertiser: NSObject, CBPeripheralManagerDelegate {
    /// Fixed UUID for the characteristic that returns the device id to a
    /// connected Windows scanner.
    static let identityCharacteristicUUID = CBUUID(string: "C0FFEE00-1111-4222-8333-444455556666")

    private var manager: CBPeripheralManager?
    private let serviceUUID: CBUUID
    private let deviceId: String

    init(deviceId: String) {
        self.deviceId = deviceId
        self.serviceUUID = CBUUID(nsuuid: ProximityUuid.serviceUUID(for: deviceId))
        super.init()
    }

    func start() {
        guard manager == nil else { return }
        manager = CBPeripheralManager(delegate: self, queue: nil)
    }

    func stop() {
        manager?.stopAdvertising()
        manager = nil
    }

    func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
        guard peripheral.state == .poweredOn else { return }

        let identity = CBMutableCharacteristic(
            type: Self.identityCharacteristicUUID,
            properties: [.read],
            value: Data(deviceId.utf8),
            permissions: [.readable])

        let service = CBMutableService(type: serviceUUID, primary: true)
        service.characteristics = [identity]
        peripheral.add(service)

        peripheral.startAdvertising([
            CBAdvertisementDataServiceUUIDsKey: [serviceUUID],
            CBAdvertisementDataLocalNameKey: deviceId
        ])
    }
}