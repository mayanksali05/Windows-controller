import { sha1 } from './sha';
import { textToUtf8 } from '../utils/time';

/**
 * RFC 4122 v5 BLE service UUID for a device. The Windows scanner derives the
 * same value for each paired device; the iPhone advertises it so it can be
 * identified by service UUID alone. Mirrors
 * `WinLock.Service.Bluetooth.ProximityUuid` byte-for-byte (RFC 4122 network
 * order bytes, version-5 / variant bits).
 */

const NAMESPACE = '9B2F6D21-8E4C-4E2A-9F6A-9D4E3B2C1A00';

function namespaceBytesInNetworkOrder(): Uint8Array {
  const hex = NAMESPACE.replace(/-/g, '');
  const bytes = new Uint8Array(16);
  for (let i = 0; i < 16; i++) {
    bytes[i] = parseInt(hex.substr(i * 2, 2), 16);
  }
  return bytes;
}

export function proximityServiceUuid(deviceId: string): string {
  const ns = namespaceBytesInNetworkOrder();
  const name = textToUtf8(deviceId);
  const buffer = new Uint8Array(ns.length + name.length);
  buffer.set(ns, 0);
  buffer.set(name, ns.length);

  const digest = sha1(buffer); // 20 bytes
  const bytes = new Uint8Array(16);
  bytes.set(digest.subarray(0, 16));
  bytes[6] = (bytes[6] & 0x0f) | 0x50; // version 5
  bytes[8] = (bytes[8] & 0x3f) | 0x80; // RFC 4122 variant

  return uuidFromNetworkBytes(bytes);
}

function uuidFromNetworkBytes(bytes: Uint8Array): string {
  const hex = Array.from(bytes)
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('');
  return (
    `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-` +
    `${hex.slice(16, 20)}-${hex.slice(20)}`
  );
}