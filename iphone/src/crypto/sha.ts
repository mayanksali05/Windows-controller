import { sha256 as sha256Hash } from '@noble/hashes/sha256';
import { sha1 as sha1Hash } from '@noble/hashes/sha1';

/** SHA-256 of a byte array. */
export function sha256(data: Uint8Array): Uint8Array {
  return sha256Hash(data);
}

/** SHA-1 of a byte array (used only for the RFC 4122 v5 BLE UUID). */
export function sha1(data: Uint8Array): Uint8Array {
  return sha1Hash(data);
}