import { WinlockBluetoothNative, type BluetoothAdapterState } from '../native/winlockBluetooth';
import { proximityServiceUuid } from '../crypto/proximityUuid';
import { deriveProximity } from './proximityState';
import type { ProximityStateString } from '../types/protocol';

/**
 * Platform-independent Bluetooth service. The iPhone is the BLE advertiser
 * (the Windows service scans); this client never scans, so BLE presence is
 * learned by reading the server-reported proximity. Proximity is a signal
 * only and never authenticates.
 */
export interface BluetoothService {
  /** Advertise the per-device service UUID derived from the device id. */
  startAdvertising(deviceId: string): void;
  stopAdvertising(): void;
  getAdapterState(): BluetoothAdapterState;
  /** Derive the combined state from the server-reported proximity. */
  getProximityState(serverState: ProximityStateString, isAuthenticated: boolean): ProximityStateString;
}

export const bluetoothService: BluetoothService = {
  startAdvertising(deviceId: string): void {
    const serviceUuid = proximityServiceUuid(deviceId);
    WinlockBluetoothNative.startAdvertising(deviceId, serviceUuid);
  },
  stopAdvertising(): void {
    WinlockBluetoothNative.stopAdvertising();
  },
  getAdapterState(): BluetoothAdapterState {
    return WinlockBluetoothNative.getState() as BluetoothAdapterState;
  },
  getProximityState(serverState: ProximityStateString, isAuthenticated: boolean): ProximityStateString {
    return deriveProximity(serverState, isAuthenticated);
  },
};