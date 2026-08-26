import CoreBluetooth
import ExpoModulesCore
import Foundation

/// Advertises the iPhone's per-device BLE service so the Windows laptop can
/// estimate proximity. The service UUID is derived on the JS side (RFC 4122 v5
/// of the device id) and passed in; this module only handles CoreBluetooth.
/// BLE presence is a proximity signal only and never authorizes anything.
public class WinlockBluetoothModule: Module, CBPeripheralManagerDelegate {
  /// Fixed UUID for the characteristic that returns the device id to a
  /// connected Windows scanner.
  private static let identityCharacteristicUUID = CBUUID(string: "C0FFEE00-1111-4222-8333-444455556666")

  private var manager: CBPeripheralManager?
  private var serviceUUID: CBUUID?
  private var deviceId: String?
  private var shouldAdvertise = false

  public func definition() -> ModuleDefinition {
    Name("WinlockBluetooth")

    Function("startAdvertising") { (deviceId: String, serviceUuid: String) in
      self.deviceId = deviceId
      self.serviceUUID = CBUUID(string: serviceUuid)
      self.shouldAdvertise = true
      if self.manager == nil {
        self.manager = CBPeripheralManager(delegate: self, queue: nil)
      } else {
        self.startAdvertisingNow()
      }
    }

    Function("stopAdvertising") {
      self.shouldAdvertise = false
      self.manager?.stopAdvertising()
    }

    Function("getState") { () -> String in
      self.stateString(self.manager?.state ?? .unknown)
    }
  }

  public func peripheralManagerDidUpdateState(_ peripheral: CBPeripheralManager) {
    if peripheral.state == .poweredOn {
      startAdvertisingNow()
    }
  }

  private func startAdvertisingNow() {
    guard shouldAdvertise,
          let deviceId = deviceId,
          let serviceUUID = serviceUUID,
          let manager = manager,
          manager.state == .poweredOn else {
      return
    }

    manager.removeAllServices()

    let identity = CBMutableCharacteristic(
      type: Self.identityCharacteristicUUID,
      properties: [.read],
      value: Data(deviceId.utf8),
      permissions: [.readable])

    let service = CBMutableService(type: serviceUUID, primary: true)
    service.characteristics = [identity]
    manager.add(service)

    manager.startAdvertising([
      CBAdvertisementDataServiceUUIDsKey: [serviceUUID],
      CBAdvertisementDataLocalNameKey: deviceId
    ])
  }

  private func stateString(_ state: CBManagerState) -> String {
    switch state {
    case .poweredOn: return "POWERED_ON"
    case .poweredOff: return "POWERED_OFF"
    case .unsupported: return "UNSUPPORTED"
    case .unauthorized: return "UNAUTHORIZED"
    case .resetting: return "RESETTING"
    default: return "UNKNOWN"
    }
  }
}