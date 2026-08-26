/**
 * Deterministic byte sequences signed with Ed25519. Must produce byte-for-byte
 * identical input to the Windows `ProtocolStrings` helpers: fields joined with
 * 0x1F (unit separator), single trailing 0x1E (record separator).
 */

export const CHALLENGE_VERIFY_ENDPOINT = '/api/v1/auth/verify';
export const PAIRING_CONFIRM_ENDPOINT = '/api/v1/pair/confirm';

const textEncoder = new TextEncoder();

export function pairingSigningInput(deviceId: string, nonce: string): Uint8Array {
  return canonical([deviceId, nonce]);
}

export function authenticationSigningInput(
  deviceId: string,
  challenge: string,
  timestamp: string,
  endpoint: string,
): Uint8Array {
  return canonical([deviceId, challenge, timestamp, endpoint]);
}

function canonical(parts: string[]): Uint8Array {
  const total = parts.reduce((sum, part) => sum + part.length, 0) + parts.length;
  const out = new Uint8Array(total);
  let offset = 0;
  for (let i = 0; i < parts.length; i++) {
    const bytes = textEncoder.encode(parts[i]);
    out.set(bytes, offset);
    offset += bytes.length;
    out[offset++] = i === parts.length - 1 ? 0x1e : 0x1f;
  }
  return out;
}