import { loadOrCreateIdentity, getPublicKey } from '../identity';
import { sha256 } from '../sha';

describe('identity', () => {
  it('derives the device id from the public key (Windows scheme)', () => {
    const seed = new Uint8Array(Array.from({ length: 32 }, (_, i) => i + 1));
    const publicKey = getPublicKey(seed);
    const expectedDigest = sha256(publicKey);
    const expectedId = Buffer.from(expectedDigest).toString('hex').slice(0, 16).toUpperCase();
    // The Windows known-answer: seed 01..20 -> device id 65B60673D6ED884B.
    expect(expectedId).toBe('65B60673D6ED884B');
  });

  it('creates a stable identity persisted in secure storage', async () => {
    const first = await loadOrCreateIdentity();
    const second = await loadOrCreateIdentity();

    expect(first.deviceId).toBe(second.deviceId);
    expect(Buffer.from(first.signer.publicKeyBytes).toString('hex')).toBe(
      Buffer.from(second.signer.publicKeyBytes).toString('hex'),
    );
    expect(first.deviceId).toMatch(/^[0-9A-F]{16}$/);
    expect(first.signer.publicKeyBytes.length).toBe(32);
  });
});