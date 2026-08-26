import { WindowsApiClient } from '../windowsApiClient';
import { WinlockNetworkingNative } from '../../native/winlockNetworking';
import { createSigner } from '../../crypto/ed25519';
import * as protocol from '../../crypto/protocolStrings';
import { base64urlDecode, base64urlEncode } from '../../crypto/base64url';
import { verify } from '../../crypto/ed25519';
import {
  AuthenticationError,
  CertificateError,
  InvalidResponseError,
  ServerError,
} from '../errors';
import type { StatusDto } from '../../types/protocol';

const pinnedRequestMock = WinlockNetworkingNative.pinnedRequest as jest.Mock;

const seed = new Uint8Array(Array.from({ length: 32 }, (_, i) => i + 1));
const signer = createSigner(seed);
const deviceId = 'ABC123DEF4567890';
const NONCE = base64urlEncode(new Uint8Array(32));

function futureIso(): string {
  return new Date(Date.now() + 10 * 60 * 1000).toISOString();
}

function makeClient() {
  const requireFaceId = jest.fn(async () => {});
  const client = new WindowsApiClient({
    baseUrl: 'https://192.168.1.2:8765',
    pin: 'cGlu',
    mode: 'development',
    deviceId,
    signer,
    requireFaceId,
  });
  return { client, requireFaceId };
}

function route(status: StatusDto) {
  pinnedRequestMock.mockImplementation(async (opts: { url: string; method: string; body?: string }) => {
    if (opts.url.endsWith('/api/v1/auth/challenge')) {
      return {
        status: 200,
        body: JSON.stringify({
          success: true,
          data: { challengeId: 'c1', challenge: NONCE, expiresAt: futureIso() },
        }),
      };
    }
    if (opts.url.endsWith('/api/v1/auth/verify')) {
      return {
        status: 200,
        body: JSON.stringify({
          success: true,
          data: { sessionToken: 'tok', sessionExpires: futureIso(), proximity: 'UNKNOWN' },
        }),
      };
    }
    if (opts.url.endsWith('/api/v1/status')) {
      return { status: 200, body: JSON.stringify({ success: true, data: status }) };
    }
    if (opts.url.endsWith('/api/v1/lock')) {
      return { status: 200, body: JSON.stringify({ success: true, message: 'Laptop locked successfully' }) };
    }
    throw new Error(`unexpected url ${opts.url}`);
  });
}

describe('WindowsApiClient', () => {
  beforeEach(() => {
    pinnedRequestMock.mockReset();
  });

  it('authenticates via signed challenge-response and returns status', async () => {
    const { client, requireFaceId } = makeClient();
    const status: StatusDto = {
      isLocked: false,
      batteryPercent: 74,
      serviceVersion: '0.1.0',
      environment: 'Development',
      lockAvailable: true,
      proximity: 'NEARBY',
      security: 'PAIRED',
    };
    route(status);

    const result = await client.getStatus();

    expect(result).toEqual(status);
    expect(requireFaceId).toHaveBeenCalled();

    // The verify request must carry a signature over the canonical string.
    const verifyCall = pinnedRequestMock.mock.calls.find((c) =>
      c[0].url.endsWith('/api/v1/auth/verify'),
    );
    const verifyBody = JSON.parse(verifyCall![0].body);
    expect(verifyBody.clientDeviceId).toBe(deviceId);
    expect(verifyBody.challengeId).toBe('c1');
    const input = protocol.authenticationSigningInput(
      deviceId,
      NONCE,
      verifyBody.timestamp,
      protocol.CHALLENGE_VERIFY_ENDPOINT,
    );
    expect(verify(signer.publicKeyBytes, base64urlDecode(verifyBody.signature), input)).toBe(true);

    // The status request must carry the bearer session token.
    const statusCall = pinnedRequestMock.mock.calls.find((c) => c[0].url.endsWith('/api/v1/status'));
    expect(statusCall![0].headers.Authorization).toBe('Bearer tok');
    expect(client.hasValidSession()).toBe(true);
  });

  it('maps a structured server error to ServerError', async () => {
    const { client } = makeClient();
    pinnedRequestMock.mockImplementation(async (opts: { url: string }) => {
      if (opts.url.endsWith('/api/v1/auth/challenge')) {
        return {
          status: 200,
          body: JSON.stringify({ success: true, data: { challengeId: 'c1', challenge: NONCE, expiresAt: futureIso() } }),
        };
      }
      if (opts.url.endsWith('/api/v1/auth/verify')) {
        return {
          status: 200,
          body: JSON.stringify({ success: true, data: { sessionToken: 'tok', sessionExpires: futureIso(), proximity: 'UNKNOWN' } }),
        };
      }
      return {
        status: 200,
        body: JSON.stringify({ success: false, error: { code: 'AUTH_FAILED', message: 'Invalid session' } }),
      };
    });

    await expect(client.getStatus()).rejects.toMatchObject({
      name: 'ServerError',
      serverCode: 'AUTH_FAILED',
    });
  });

  it('re-authenticates once on a 401 and retries', async () => {
    const { client, requireFaceId } = makeClient();
    let statusCalls = 0;
    pinnedRequestMock.mockImplementation(async (opts: { url: string }) => {
      if (opts.url.endsWith('/api/v1/auth/challenge')) {
        return { status: 200, body: JSON.stringify({ success: true, data: { challengeId: 'c1', challenge: NONCE, expiresAt: futureIso() } }) };
      }
      if (opts.url.endsWith('/api/v1/auth/verify')) {
        return { status: 200, body: JSON.stringify({ success: true, data: { sessionToken: 'tok', sessionExpires: futureIso(), proximity: 'UNKNOWN' } }) };
      }
      if (opts.url.endsWith('/api/v1/status')) {
        statusCalls += 1;
        if (statusCalls === 1) {
          return { status: 401, body: JSON.stringify({ success: false, error: { code: 'AUTH_FAILED', message: 'expired' } }) };
        }
        return {
          status: 200,
          body: JSON.stringify({
            success: true,
            data: { isLocked: false, batteryPercent: null, serviceVersion: '0.1.0', environment: 'Development', lockAvailable: true, proximity: 'UNKNOWN', security: 'PAIRED' },
          }),
        };
      }
      throw new Error('unexpected');
    });

    const result = await client.getStatus();
    expect(result.serviceVersion).toBe('0.1.0');
    expect(requireFaceId).toHaveBeenCalledTimes(2);
    expect(statusCalls).toBe(2);
  });

  it('throws InvalidResponseError on a malformed body', async () => {
    const { client } = makeClient();
    pinnedRequestMock.mockImplementation(async (opts: { url: string }) => {
      if (opts.url.endsWith('/api/v1/auth/challenge')) {
        return { status: 200, body: JSON.stringify({ success: true, data: { challengeId: 'c1', challenge: NONCE, expiresAt: futureIso() } }) };
      }
      if (opts.url.endsWith('/api/v1/auth/verify')) {
        return { status: 200, body: JSON.stringify({ success: true, data: { sessionToken: 'tok', sessionExpires: futureIso(), proximity: 'UNKNOWN' } }) };
      }
      return { status: 200, body: '{ not json' };
    });

    await expect(client.getStatus()).rejects.toBeInstanceOf(InvalidResponseError);
  });

  it('maps a certificate rejection to CertificateError', async () => {
    const { client } = makeClient();
    pinnedRequestMock.mockRejectedValue(Object.assign(new Error('TLS certificate did not match'), { code: 'ERR_CERTIFICATE' }));

    await expect(client.getStatus()).rejects.toBeInstanceOf(CertificateError);
  });

  it('lock succeeds and requires no data payload', async () => {
    const { client } = makeClient();
    route({
      isLocked: false,
      batteryPercent: null,
      serviceVersion: '0.1.0',
      environment: 'Development',
      lockAvailable: true,
      proximity: 'UNKNOWN',
      security: 'PAIRED',
    });
    await client.lock();
    const lockCall = pinnedRequestMock.mock.calls.find((c) => c[0].url.endsWith('/api/v1/lock'));
    expect(lockCall).toBeDefined();
  });

  it('pairConfirm sends a signed body over canonical(deviceId, nonce)', async () => {
    const { client } = makeClient();
    const payload = {
      version: 1,
      deviceId: 'WINDEV1234567890',
      windowsPublicKey: signer.publicKeyBase64Url,
      pairingNonce: 'bm9uY2U',
      pairingToken: 'one-time-token',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      signature: '',
      tlsPin: 'cGlu',
    };

    pinnedRequestMock.mockImplementation(async (opts: { url: string; body?: string }) => {
      if (opts.url.endsWith('/api/v1/pair/confirm')) {
        return { status: 200, body: JSON.stringify({ success: true, message: 'Pairing completed' }) };
      }
      throw new Error('unexpected');
    });

    await client.pairConfirm(payload);

    const call = pinnedRequestMock.mock.calls.find((c) => c[0].url.endsWith('/api/v1/pair/confirm'));
    const body = JSON.parse(call![0].body!) as {
      deviceId: string;
      clientDeviceId: string;
      clientPublicKey: string;
      pairingToken: string;
      signature: string;
    };
    expect(body.deviceId).toBe('WINDEV1234567890');
    expect(body.clientDeviceId).toBe(deviceId);
    expect(body.clientPublicKey).toBe(signer.publicKeyBase64Url);
    expect(body.pairingToken).toBe('one-time-token');

    const input = protocol.pairingSigningInput(deviceId, payload.pairingNonce);
    expect(verify(signer.publicKeyBytes, base64urlDecode(body.signature), input)).toBe(true);
  });
});