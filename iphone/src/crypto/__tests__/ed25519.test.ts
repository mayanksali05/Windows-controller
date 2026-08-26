import { getPublicKey, sign, verify, randomSeed } from '../ed25519';
import { hexToBytes } from '../../utils/hex';
import { pairingSigningInput } from '../protocolStrings';

const textEncoder = new TextEncoder();

describe('ed25519', () => {
  it('matches the Windows known-answer vector (seed 01..20)', () => {
    const seed = new Uint8Array(Array.from({ length: 32 }, (_, i) => i + 1));
    const publicKey = getPublicKey(seed);
    const message = new Uint8Array(textEncoder.encode('DEVICE\u001fNONCE\u001e'));

    const sig = sign(seed, message);
    expect(Buffer.from(publicKey).toString('hex').toUpperCase()).toBe(
      '79B5562E8FE654F94078B112E8A98BA7901F853AE695BED7E0E3910BAD049664',
    );
    expect(Buffer.from(sig).toString('hex').toUpperCase()).toBe(
      '6C82F6CCB54A869C1342505EE91A1B4220FFD19CABFEEFBBAD626326133F6209D2B3E921BAD48150070BD24AB9B4C4338D437D94F0773E4F07EEEECBDF4A5D06',
    );
    expect(verify(publicKey, sig, message)).toBe(true);
  });

  it('matches RFC 8032 test vector (empty message)', () => {
    const seed = hexToBytes('9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60');
    const publicKey = getPublicKey(seed);
    const message = new Uint8Array();
    const sig = sign(seed, message);

    expect(Buffer.from(publicKey).toString('hex')).toBe(
      'd75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a',
    );
    expect(Buffer.from(sig).toString('hex')).toBe(
      'e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e06522490155' +
        '5fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b',
    );
    expect(verify(publicKey, sig, message)).toBe(true);
  });

  it('rejects tampered messages and signatures', () => {
    const seed = new Uint8Array(Array.from({ length: 32 }, (_, i) => i + 1));
    const publicKey = getPublicKey(seed);
    const message = pairingSigningInput('DEVICE', 'NONCE');
    const sig = sign(seed, message);

    const tampered = pairingSigningInput('EVICE', 'NONCE');
    expect(verify(publicKey, sig, tampered)).toBe(false);

    const badSig = new Uint8Array(sig);
    badSig[0] ^= 0xff;
    expect(verify(publicKey, badSig, message)).toBe(false);
  });

  it('rejects malformed keys and signatures', () => {
    expect(verify(new Uint8Array(16), new Uint8Array(64), new Uint8Array(4))).toBe(false);
    expect(verify(new Uint8Array(32), new Uint8Array(32), new Uint8Array(4))).toBe(false);
  });

  it('generates a fresh 32-byte seed', async () => {
    const seed = await randomSeed();
    expect(seed.length).toBe(32);
  });
});