/* eslint-disable @typescript-eslint/no-var-requires */
import { jest } from '@jest/globals';

// In-memory Keychain for tests.
jest.mock('expo-secure-store', () => {
  const store = new Map<string, string>();
  return {
    setItemAsync: jest.fn(async (key: string, value: string) => {
      store.set(key, String(value));
    }),
    getItemAsync: jest.fn(async (key: string) => store.get(key) ?? null),
    deleteItemAsync: jest.fn(async (key: string) => {
      store.delete(key);
    }),
    AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY: 'afterFirstUnlockThisDeviceOnly',
  };
});

// CSPRNG mock (deterministic bytes for reproducible tests).
jest.mock('expo-crypto', () => ({
  getRandomBytesAsync: jest.fn(async (length: number) => {
    const bytes = new Uint8Array(length);
    for (let i = 0; i < length; i++) {
      bytes[i] = (i + 1) & 0xff;
    }
    return bytes;
  }),
}));

jest.mock('expo-local-authentication', () => ({
  hasHardwareAsync: jest.fn(async () => true),
  isEnrolledAsync: jest.fn(async () => true),
  authenticateAsync: jest.fn(async () => ({ success: true })),
  supportedAuthenticationTypesAsync: jest.fn(async () => []),
}));

jest.mock('@react-native-async-storage/async-storage', () => {
  let data: Record<string, string> = {};
  return {
    setItem: jest.fn(async (key: string, value: string) => {
      data[key] = String(value);
    }),
    getItem: jest.fn(async (key: string) => data[key] ?? null),
    removeItem: jest.fn(async (key: string) => {
      delete data[key];
    }),
    clear: jest.fn(async () => {
      data = {};
    }),
  };
});

jest.mock('./src/native/winlockNetworking', () => ({
  WinlockNetworkingNative: {
    pinnedRequest: jest.fn(),
    startDiscovery: jest.fn(),
    stopDiscovery: jest.fn(),
  },
  onLaptopDiscovered: jest.fn(() => () => {}),
}));

jest.mock('./src/native/winlockBluetooth', () => ({
  WinlockBluetoothNative: {
    startAdvertising: jest.fn(),
    stopAdvertising: jest.fn(),
    getState: jest.fn(() => 'POWERED_ON'),
  },
}));