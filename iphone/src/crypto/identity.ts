import { randomSeed, getPublicKey, createSigner, type Signer } from './ed25519';
import { sha256 } from './sha';
import { bytesToHex } from '../utils/hex';
import { secureGet, secureSet, SECURE_KEYS } from '../storage/secureStore';

/**
 * The iPhone identity: an Ed25519 key pair stored in the iOS Keychain via
 * expo-secure-store (never AsyncStorage, never plaintext files). The device id
 * is derived from the public key (HEX(SHA256(pub))[0..8]) so it is stable and
 * bound to the key, mirroring the Windows laptop identity scheme.
 */

const PRIVATE_KEY_STORE_KEY = SECURE_KEYS.identitySeed;

export interface DeviceIdentity {
  deviceId: string;
  signer: Signer;
}

function deriveDeviceId(publicKeyBytes: Uint8Array): string {
  const digest = sha256(publicKeyBytes);
  return bytesToHex(digest).slice(0, 16).toUpperCase();
}

/** Load the existing identity or create and persist a fresh one. */
export async function loadOrCreateIdentity(): Promise<DeviceIdentity> {
  const stored = await secureGet(PRIVATE_KEY_STORE_KEY);
  if (stored) {
    const seed = decodeSeed(stored);
    const signer = createSigner(seed);
    return { deviceId: deriveDeviceId(signer.publicKeyBytes), signer };
  }

  const seed = await randomSeed();
  await secureSet(PRIVATE_KEY_STORE_KEY, encodeSeed(seed));
  const signer = createSigner(seed);
  return { deviceId: deriveDeviceId(signer.publicKeyBytes), signer };
}

function encodeSeed(seed: Uint8Array): string {
  // Hex avoids any chance of the base64 alphabet colliding with Keychain/JSON
  // escaping and is unambiguous.
  return bytesToHex(seed);
}

function decodeSeed(encoded: string): Uint8Array {
  const bytes = new Uint8Array(encoded.length / 2);
  for (let i = 0; i < bytes.length; i++) {
    bytes[i] = parseInt(encoded.substr(i * 2, 2), 16);
  }
  if (bytes.length !== 32) {
    throw new Error('Stored identity seed has an invalid length');
  }
  return bytes;
}

// Re-exported for tests/known-answer checks.
export { getPublicKey, randomSeed };