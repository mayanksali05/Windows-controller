import type { WindowsApiClient } from '../api/windowsApiClient';
import type { DeviceIdentity } from '../crypto/identity';
import * as protocol from '../crypto/protocolStrings';
import { base64urlDecode } from '../crypto/base64url';
import { verify as ed25519Verify } from '../crypto/ed25519';
import { InvalidPairingPayloadError, AuthenticationError } from '../api/errors';
import { storePairedLaptop, removeLaptop, loadLaptops } from '../storage/laptopStore';
import type { FaceIdGate } from '../auth/faceIdGate';
import type { PairingSessionPayload, PairedLaptop, DiscoveredLaptop } from '../types/protocol';
import type { BluetoothService } from '../bluetooth/bluetoothService';

export type PairingStage = 'idle' | 'verifying' | 'confirming' | 'done';

/** Minimal client surface needed to confirm pairing (no session required). */
export interface PairingClient {
  pairConfirm(payload: PairingSessionPayload): Promise<void>;
}

/**
 * Implements the existing pairing protocol exactly:
 *   discover -> select -> scan QR (payload) -> verify Windows signature ->
 *   Face ID -> signed pair/confirm -> persist trust -> advertise BLE.
 */
export class PairingService {
  constructor(
    private readonly identity: DeviceIdentity,
    private readonly faceId: FaceIdGate,
    private readonly bluetooth: BluetoothService,
    private readonly onEvent?: (kind: string, message: string) => void,
  ) {}

  /** Parse and structurally validate a scanned/pasted pairing payload. */
  parsePayload(raw: string): PairingSessionPayload {
    let payload: PairingSessionPayload;
    try {
      payload = JSON.parse(raw) as PairingSessionPayload;
    } catch {
      throw new InvalidPairingPayloadError();
    }
    const required: (keyof PairingSessionPayload)[] = [
      'version',
      'deviceId',
      'windowsPublicKey',
      'pairingNonce',
      'pairingToken',
      'expiresAt',
      'signature',
      'tlsPin',
    ];
    for (const key of required) {
      if (typeof payload[key] !== 'string' && key !== 'version') {
        throw new InvalidPairingPayloadError();
      }
      if (key === 'version' && typeof payload[key] !== 'number') {
        throw new InvalidPairingPayloadError();
      }
    }
    if (typeof payload.tlsPin !== 'string' || payload.tlsPin.length === 0) {
      throw new InvalidPairingPayloadError();
    }
    return payload;
  }

  /** Verify the Windows identity: signature over canonical(deviceId, nonce). */
  verifyWindowsIdentity(payload: PairingSessionPayload): boolean {
    try {
      const publicKey = base64urlDecode(payload.windowsPublicKey);
      const signature = base64urlDecode(payload.signature);
      const input = protocol.pairingSigningInput(payload.deviceId, payload.pairingNonce);
      return ed25519Verify(publicKey, signature, input);
    } catch {
      return false;
    }
  }

  /** Full pairing flow for a discovered laptop and a scanned payload. */
  async pair(
    laptop: DiscoveredLaptop,
    rawPayload: string,
    createPairingClient: (host: string, port: number, pin: string) => PairingClient,
  ): Promise<PairedLaptop> {
    const payload = this.parsePayload(rawPayload);

    if (!this.verifyWindowsIdentity(payload)) {
      this.onEvent?.('PAIRING_FAILED', 'Pairing payload signature verification failed');
      throw new AuthenticationError('Pairing payload signature is invalid');
    }

    await this.faceId.require('Pair with your Windows laptop');

    this.onEvent?.('PAIRING_STARTED', 'Confirming pairing');
    const client = createPairingClient(laptop.host, laptop.port, payload.tlsPin);
    try {
      await client.pairConfirm(payload);
    } catch (error) {
      this.onEvent?.('PAIRING_FAILED', 'Pairing confirm failed');
      throw error;
    }

    const paired: PairedLaptop = {
      deviceId: payload.deviceId,
      name: laptop.name,
      host: laptop.host,
      port: laptop.port,
      pairedAt: new Date().toISOString(),
    };
    await storePairedLaptop(paired, payload.windowsPublicKey, payload.tlsPin);
    this.bluetooth.startAdvertising(this.identity.deviceId);
    this.onEvent?.('PAIRING_COMPLETED', `Paired with ${paired.name}`);

    return paired;
  }

  /** Face-ID-gated unpairing; stops advertising when no laptops remain. */
  async unpair(deviceId: string, client: WindowsApiClient): Promise<void> {
    await this.faceId.require('Unpair this Windows laptop');
    await client.unpair(deviceId);
    await removeLaptop(deviceId);

    const remaining = await loadLaptops();
    if (remaining.length === 0) {
      this.bluetooth.stopAdvertising();
    }
    this.onEvent?.('DEVICE_UNPAIRED', `Unpaired device ${deviceId}`);
  }
}