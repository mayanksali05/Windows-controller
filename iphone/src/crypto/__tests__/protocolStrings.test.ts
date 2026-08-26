import { pairingSigningInput, authenticationSigningInput, CHALLENGE_VERIFY_ENDPOINT } from '../protocolStrings';

describe('protocolStrings (canonical signing inputs)', () => {
  it('builds the pairing canonical input (fields joined 0x1f, trailing 0x1e)', () => {
    const input = pairingSigningInput('DEVICE', 'NONCE');
    expect(Array.from(input)).toEqual([
      ...Array.from(new TextEncoder().encode('DEVICE')),
      0x1f,
      ...Array.from(new TextEncoder().encode('NONCE')),
      0x1e,
    ]);
  });

  it('builds the authentication canonical input', () => {
    const input = authenticationSigningInput('DEV', 'CHALLENGE', '2026-08-25T00:00:00Z', CHALLENGE_VERIFY_ENDPOINT);
    const expected = new TextEncoder().encode(
      'DEV\u001fCHALLENGE\u001f2026-08-25T00:00:00Z\u001f/api/v1/auth/verify\u001e',
    );
    expect(input).toEqual(expected);
  });

  it('is deterministic', () => {
    const a = pairingSigningInput('X', 'Y');
    const b = pairingSigningInput('X', 'Y');
    expect(a).toEqual(b);
  });
});