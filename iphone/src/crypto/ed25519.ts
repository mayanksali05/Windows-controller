import * as ed25519 from '@noble/ed25519';
import { sha512 } from '@noble/hashes/sha512';
import * as Crypto from 'expo-crypto';
import { base64urlEncode } from './base64url';

/**
 * Ed25519 (RFC 8032) primitives interoperable with the Windows BouncyCastle
 * implementation: raw 32-byte private seed, 32-byte public key, 64-byte
 * deterministic signatures.
 *
 * @noble/ed25519 v2 requires the SHA-512 hash to be wired in explicitly.
 */
ed25519.etc.sha512Sync = (...messages: Uint8Array[]) =>
  sha512(ed25519.etc.concatBytes(...messages));
ed25519.etc.sha512Async = (...messages: Uint8Array[]) =>
  Promise.resolve((ed25519.etc.sha512Sync as (...m: Uint8Array[]) => Uint8Array)(...messages));

export const PUBLIC_KEY_BYTES = 32;
export const SIGNATURE_BYTES = 64;

export interface Signer {
  publicKeyBytes: Uint8Array;
  publicKeyBase64Url: string;
  sign(message: Uint8Array): Promise<Uint8Array>;
}

/** Generate a fresh 32-byte Ed25519 seed using a CSPRNG (expo-crypto). */
export async function randomSeed(): Promise<Uint8Array> {
  const bytes = await Crypto.getRandomBytesAsync(32);
  return new Uint8Array(bytes);
}

export function getPublicKey(seed: Uint8Array): Uint8Array {
  return ed25519.getPublicKey(seed);
}

export function sign(seed: Uint8Array, message: Uint8Array): Uint8Array {
  return ed25519.sign(message, seed);
}

export function verify(
  publicKey: Uint8Array,
  signature: Uint8Array,
  message: Uint8Array,
): boolean {
  if (publicKey.length !== PUBLIC_KEY_BYTES || signature.length !== SIGNATURE_BYTES) {
    return false;
  }
  try {
    return ed25519.verify(signature, message, publicKey);
  } catch {
    return false;
  }
}

/** Wrap a seed as a Signer. */
export function createSigner(seed: Uint8Array): Signer {
  const publicKeyBytes = getPublicKey(seed);
  return {
    publicKeyBytes,
    publicKeyBase64Url: base64urlEncode(publicKeyBytes),
    sign: async (message: Uint8Array) => sign(seed, message),
  };
}