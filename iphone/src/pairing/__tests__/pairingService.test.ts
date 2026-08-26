import { PairingService } from '../pairingService';
import { createSigner, sign } from '../../crypto/ed25519';
import * as protocol from '../../crypto/protocolStrings';
import { base64urlEncode } from '../../crypto/base64url';
import { loadLaptops } from '../../storage/laptopStore';
import { AuthenticationError, InvalidPairingPayloadError } from '../../api/errors';
import type { DeviceIdentity } from '../../crypto/identity';
import type { DiscoveredLaptop, PairingSessionPayload } from '../../types/protocol';

const seed = new Uint8Array(Array.from({ length: 32 }, (_, i) => i + 1));
const windowsSigner = createSigner(seed);
const identity: DeviceIdentity = {
  deviceId: 'PHONE12345678ABCD',
  signer: createSigner(new Uint8Array(Array.from({ length: 32 }, (_, i) => i + 2))),
};

const laptop: DiscoveredLaptop = {
  name: 'WinLock-ABC123',
  deviceId: 'WINDEV1234567890',
  host: '192.168.1.2',
  port: 8765,
};

function validPayload(): PairingSessionPayload {
  const nonce = 'bm9uY2UtMQ';
  return {
    version: 1,
    deviceId: laptop.deviceId,
    windowsPublicKey: windowsSigner.publicKeyBase64Url,
    pairingNonce: nonce,
    pairingToken: 'one-time-token',
    expiresAt: new Date(Date.now() + 60_000).toISOString(),
    signature: base64urlEncode(sign(seed, protocol.pairingSigningInput(laptop.deviceId, nonce))),
    tlsPin: 'cGluLWRlcg',
  };
}

function makeService() {
  const faceId = { require: jest.fn(async () => {}) };
  const bluetooth = { startAdvertising: jest.fn(), stopAdvertising: jest.fn(), getAdapterState: jest.fn(), getProximityState: jest.fn() };
  const events: string[] = [];
  const service = new PairingService(identity, faceId, bluetooth, (kind, message) => events.push(`${kind}: ${message}`));
  return { service, faceId, bluetooth, events };
}

describe('PairingService', () => {
  it('parses a valid payload', () => {
    const { service } = makeService();
    expect(service.parsePayload(JSON.stringify(validPayload())).deviceId).toBe(laptop.deviceId);
  });

  it('rejects malformed payloads', () => {
    const { service } = makeService();
    expect(() => service.parsePayload('not json')).toThrow(InvalidPairingPayloadError);
    expect(() => service.parsePayload(JSON.stringify({ ...validPayload(), tlsPin: '' }))).toThrow(
      InvalidPairingPayloadError,
    );
    expect(() => service.parsePayload(JSON.stringify({ ...validPayload(), pairingToken: undefined }))).toThrow(
      InvalidPairingPayloadError,
    );
  });

  it('verifies the Windows identity signature', () => {
    const { service } = makeService();
    expect(service.verifyWindowsIdentity(validPayload())).toBe(true);

    const tampered = { ...validPayload(), pairingNonce: 'tampered' };
    expect(service.verifyWindowsIdentity(tampered)).toBe(false);
  });

  it('pairs: face id, signed confirm, persist, start advertising', async () => {
    const { service, faceId, bluetooth, events } = makeService();
    const pairConfirm = jest.fn(async (_payload: PairingSessionPayload) => {});
    const fakeClient = { pairConfirm } as never;
    const createPairingClient = jest.fn(() => fakeClient);

    const paired = await service.pair(laptop, JSON.stringify(validPayload()), createPairingClient);

    expect(createPairingClient).toHaveBeenCalledWith('192.168.1.2', 8765, 'cGluLWRlcg');
    expect(faceId.require).toHaveBeenCalled();
    expect(pairConfirm).toHaveBeenCalledTimes(1);

    // The client receives the parsed payload; the signed confirm body is built
    // and tested on WindowsApiClient.pairConfirm.
    const payloadArg: PairingSessionPayload = pairConfirm.mock.calls[0][0];
    expect(payloadArg.deviceId).toBe(laptop.deviceId);
    expect(payloadArg.pairingToken).toBe('one-time-token');

    expect(bluetooth.startAdvertising).toHaveBeenCalledWith(identity.deviceId);

    const stored = await loadLaptops();
    expect(stored).toContainEqual(expect.objectContaining({ deviceId: laptop.deviceId, host: '192.168.1.2' }));
    expect(events).toContain('PAIRING_COMPLETED: Paired with WinLock-ABC123');
    expect(paired.deviceId).toBe(laptop.deviceId);
  });

  it('rejects a payload with an invalid Windows signature before confirming', async () => {
    const { service, faceId } = makeService();
    const pairConfirm = jest.fn();
    const createPairingClient = jest.fn(() => ({ pairConfirm }));

    const bad = { ...validPayload(), signature: base64urlEncode(new Uint8Array(64)) };
    await expect(service.pair(laptop, JSON.stringify(bad), createPairingClient)).rejects.toBeInstanceOf(
      AuthenticationError,
    );
    expect(createPairingClient).not.toHaveBeenCalled();
    expect(faceId.require).not.toHaveBeenCalled();
  });
});