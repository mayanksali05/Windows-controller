/**
 * WEB PREVIEW ONLY — not a supported target. The real iOS implementation lives
 * in winlockBluetooth.ts (CoreBluetooth native module). On web there is no BLE.
 */
export type BluetoothAdapterState =
  | 'POWERED_ON'
  | 'POWERED_OFF'
  | 'UNSUPPORTED'
  | 'UNAUTHORIZED'
  | 'RESETTING'
  | 'UNKNOWN';

export const WinlockBluetoothNative = {
  startAdvertising: (_deviceId: string, _serviceUuid: string): void => {},
  stopAdvertising: (): void => {},
  getState: (): string => 'UNSUPPORTED',
};