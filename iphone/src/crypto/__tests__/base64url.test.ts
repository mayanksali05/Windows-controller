import { base64urlEncode, base64urlDecode } from '../base64url';

describe('base64url', () => {
  it('round-trips arbitrary bytes', () => {
    const bytes = new Uint8Array([0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0xfb, 0xff]);
    expect(base64urlDecode(base64urlEncode(bytes))).toEqual(bytes);
  });

  it('round-trips empty input', () => {
    expect(base64urlEncode(new Uint8Array())).toBe('');
    expect(base64urlDecode('')).toEqual(new Uint8Array());
  });

  it('uses the unpadded URL-safe alphabet', () => {
    // Bytes that force + and / in standard base64 become - and _.
    const bytes = new Uint8Array([0xfb, 0xff, 0x00]);
    const encoded = base64urlEncode(bytes);
    expect(encoded).not.toContain('+');
    expect(encoded).not.toContain('/');
    expect(encoded).not.toContain('=');
    expect(base64urlDecode(encoded)).toEqual(bytes);
  });

  it('decodes standard base64 too', () => {
    const encoded = Buffer.from([0xfb, 0xff, 0x00]).toString('base64');
    expect(base64urlDecode(encoded)).toEqual(new Uint8Array([0xfb, 0xff, 0x00]));
  });

  it('rejects malformed input', () => {
    expect(() => base64urlDecode('a')).toThrow();
  });
});