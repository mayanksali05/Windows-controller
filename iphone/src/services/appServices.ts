import { Platform } from 'react-native';
import type { WindowsApiClient } from '../api/windowsApiClient';
import { WindowsApiClient as ApiClientImpl } from '../api/windowsApiClient';
import type { DeviceIdentity } from '../crypto/identity';
import { loadOrCreateIdentity } from '../crypto/identity';
import { faceIdGate } from '../auth/faceIdGate';
import { bluetoothService, type BluetoothService } from '../bluetooth/bluetoothService';
import { bonjourService, type DiscoveryService } from '../discovery/bonjourService';
import { PairingService } from '../pairing/pairingService';
import { createLogStore, type LogStore } from './logStore';
import { getTlsPin, loadLaptops, addLaptop } from '../storage/laptopStore';
import type { PairedLaptop } from '../types/protocol';

export interface AppServices {
  identity: DeviceIdentity;
  logStore: LogStore;
  bluetooth: BluetoothService;
  discovery: DiscoveryService;
  pairing: PairingService;
  /** Build an authenticated client for a paired laptop (pinned). */
  createClient(laptop: PairedLaptop): Promise<WindowsApiClient>;
  /** Build a client for pairing from the QR payload pin. */
  createPairingClient(host: string, port: number, pin: string): WindowsApiClient;
}

/**
 * Central container. Loads the iPhone identity from the Keychain and wires the
 * security, networking, discovery, BLE, and pairing services.
 */
export async function createAppServices(onEvent?: (kind: string, message: string) => void): Promise<AppServices> {
  const identity = await loadOrCreateIdentity();
  const logStore = createLogStore();

  // WEB PREVIEW ONLY: seed a clearly-labeled demo laptop so the list/detail UI
  // can be inspected in a browser. Never runs on iOS and never touches a real
  // laptop (TEST-NET address, no TLS pin, so all network calls fail cleanly).
  if (Platform.OS === 'web') {
    const existing = await loadLaptops();
    if (!existing.some((l) => l.deviceId === 'DEMO-WEB-PREVIEW')) {
      await addLaptop({
        deviceId: 'DEMO-WEB-PREVIEW',
        name: 'Demo Laptop (web preview)',
        host: '192.0.2.1',
        port: 8765,
        pairedAt: new Date().toISOString(),
      });
      logStore.add('DEMO', 'Web preview: seeded a demo laptop (no real backend).');
    }
  }

  const pairing = new PairingService(
    identity,
    faceIdGate,
    bluetoothService,
    (kind, message) => {
      logStore.add(kind, message);
      onEvent?.(kind, message);
    },
  );

  return {
    identity,
    logStore,
    bluetooth: bluetoothService,
    discovery: bonjourService,
    pairing,
    async createClient(laptop: PairedLaptop): Promise<WindowsApiClient> {
      const pin = await getTlsPin(laptop.deviceId);
      if (!pin) {
        throw new Error('TLS pin missing for paired laptop');
      }
      return new ApiClientImpl({
        baseUrl: `https://${laptop.host}:${laptop.port}`,
        pin,
        mode: 'development',
        deviceId: identity.deviceId,
        signer: identity.signer,
        requireFaceId: () => faceIdGate.require('Authenticate to WinLock'),
        onEvent: (kind, message) => logStore.add(kind, message),
      });
    },
    createPairingClient(host: string, port: number, pin: string): WindowsApiClient {
      return new ApiClientImpl({
        baseUrl: `https://${host}:${port}`,
        pin,
        mode: 'development',
        deviceId: identity.deviceId,
        signer: identity.signer,
        requireFaceId: () => faceIdGate.require('Authenticate to WinLock'),
        onEvent: (kind, message) => logStore.add(kind, message),
      });
    },
  };
}