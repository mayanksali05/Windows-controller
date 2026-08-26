/** URL-safe base64 (RFC 4648 §5), matching the Windows Base64Url helper. */

const B64_CHARS =
  'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_';

const STD_CHARS =
  'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/';

/** Encode bytes as unpadded URL-safe base64. */
export function base64urlEncode(bytes: Uint8Array): string {
  let out = '';
  for (let i = 0; i < bytes.length; i += 3) {
    const b0 = bytes[i];
    const b1 = i + 1 < bytes.length ? bytes[i + 1] : 0;
    const b2 = i + 2 < bytes.length ? bytes[i + 2] : 0;
    out += B64_CHARS[b0 >> 2];
    out += B64_CHARS[((b0 & 0x03) << 4) | (b1 >> 4)];
    if (i + 1 < bytes.length) {
      out += B64_CHARS[((b1 & 0x0f) << 2) | (b2 >> 6)];
    }
    if (i + 2 < bytes.length) {
      out += B64_CHARS[b2 & 0x3f];
    }
  }
  return out;
}

const B64_LOOKUP: Record<string, number> = (() => {
  const map: Record<string, number> = {};
  for (let i = 0; i < STD_CHARS.length; i++) {
    map[STD_CHARS[i]] = i;
  }
  return map;
})();

/**
 * Decode base64 into bytes. Accepts URL-safe input (unpadded) and standard
 * base64 (with optional '=' padding).
 */
export function base64urlDecode(value: string): Uint8Array {
  const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
  const chars = normalized.replace(/[^A-Za-z0-9+/=]/g, '');

  let str = chars;
  if (!str.includes('=')) {
    const remainder = str.length % 4;
    if (remainder === 1) {
      throw new Error('Invalid base64 length');
    }
    if (remainder === 2) {
      str += '==';
    } else if (remainder === 3) {
      str += '=';
    }
  }

  const bytes: number[] = [];
  let buffer = 0;
  let bits = 0;
  for (const ch of str) {
    if (ch === '=') {
      break;
    }
    const value = B64_LOOKUP[ch];
    if (value === undefined) {
      throw new Error(`Invalid base64 character: ${ch}`);
    }
    buffer = (buffer << 6) | value;
    bits += 6;
    if (bits >= 8) {
      bits -= 8;
      bytes.push((buffer >> bits) & 0xff);
    }
  }
  return new Uint8Array(bytes);
}