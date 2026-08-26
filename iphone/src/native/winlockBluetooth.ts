import { requireNativeModule } from 'expo-modules-core';

interface WinlockBluetoothNativeModule {
  startAdvertising(deviceId: string, serviceUuid: string): void;
  stopAdvertising(): void;
  getState(): string;
}

export type BluetoothAdapterState =
  | 'POWERED_ON'
  | 'POWERED_OFF'
  | 'UNSUPPORTED'
  | 'UNAUTHORIZED'
  | 'RESETTING'
  | 'UNKNOWN';

export const WinlockBluetoothNative =
  requireNativeModule<WinlockBluetoothNativeModule>('WinlockBluetooth');