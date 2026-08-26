import type { Signer } from '../crypto/ed25519';
import * as protocol from '../crypto/protocolStrings';
import { base64urlEncode } from '../crypto/base64url';
import { WinlockNetworkingNative } from '../native/winlockNetworking';
import {
  AuthenticationError,
  CertificateError,
  InvalidResponseError,
  NetworkError,
  ServerError,
} from './errors';
import type {
  ApiResponse,
  AuthChallengeDto,
  AuthVerifyResponseDto,
  AuthorizedDeviceDto,
  PairingSessionPayload,
  ProximityDto,
  SettingsDto,
  StatusDto,
} from '../types/protocol';

export interface WindowsApiClientOptions {
  baseUrl: string;
  pin: string;
  mode: 'development' | 'production';
  deviceId: string;
  signer: Signer;
  /** Face ID gate invoked before signing the authentication challenge. */
  requireFaceId: () => Promise<void>;
  /** Optional security-event callback (never receives secrets). */
  onEvent?: (kind: string, message: string) => void;
}

interface SendOptions {
  method: string;
  body?: Record<string, unknown>;
  authenticated: boolean;
  retried?: boolean;
}

/**
 * Typed client for the WinLock service. All requests go through the pinned
 * native HTTPS transport; privileged requests require a session token obtained
 * by the Face-ID-gated challenge-response flow, with a single transparent
 * re-authentication on 401.
 */
export class WindowsApiClient {
  private sessionToken: string | null = null;
  private sessionExpiresAt: number | null = null;

  constructor(private readonly options: WindowsApiClientOptions) {}

  async getStatus(): Promise<StatusDto> {
    return this.unwrap(await this.send<StatusDto>('/api/v1/status', { method: 'GET', authenticated: true }));
  }

  async getSettings(): Promise<SettingsDto> {
    return this.unwrap(await this.send<SettingsDto>('/api/v1/settings', { method: 'GET', authenticated: true }));
  }

  async getProximity(): Promise<ProximityDto> {
    return this.unwrap(await this.send<ProximityDto>('/api/v1/proximity', { method: 'GET', authenticated: true }));
  }

  async listDevices(): Promise<AuthorizedDeviceDto[]> {
    const response = await this.send<AuthorizedDeviceDto[]>('/api/v1/pair/devices', {
      method: 'GET',
      authenticated: true,
    });
    return this.unwrap(response) ?? [];
  }

  async lock(): Promise<void> {
    const response = await this.send<undefined>('/api/v1/lock', {
      method: 'POST',
      body: { deviceId: this.options.deviceId },
      authenticated: true,
    });
    this.requireSuccess(response);
  }

  async unpair(deviceId: string): Promise<void> {
    const response = await this.send<undefined>('/api/v1/unpair', {
      method: 'POST',
      body: { deviceId },
      authenticated: true,
    });
    this.requireSuccess(response);
  }

  /** Used during pairing: confirms the scanned payload without a session. */
  async pairConfirm(payload: PairingSessionPayload): Promise<void> {
    const input = protocol.pairingSigningInput(this.options.deviceId, payload.pairingNonce);
    const signature = await this.options.signer.sign(input);
    const response = await this.send<undefined>('/api/v1/pair/confirm', {
      method: 'POST',
      body: {
        deviceId: payload.deviceId,
        clientDeviceId: this.options.deviceId,
        clientPublicKey: this.options.signer.publicKeyBase64Url,
        pairingToken: payload.pairingToken,
        signature: base64urlEncode(signature),
      },
      authenticated: false,
    });
    this.requireSuccess(response);
  }

  /** Challenge the server for a one-time nonce (anonymous). */
  async requestChallenge(): Promise<AuthChallengeDto> {
    const response = await this.send<AuthChallengeDto>('/api/v1/auth/challenge', {
      method: 'POST',
      body: { deviceId: this.options.deviceId },
      authenticated: false,
    });
    return this.unwrap(response);
  }

  /** Submit a signed challenge and obtain a session token (anonymous). */
  async verifyAndGetSession(
    clientDeviceId: string,
    challengeId: string,
    timestamp: string,
    signature: string,
  ): Promise<AuthVerifyResponseDto> {
    const response = await this.send<AuthVerifyResponseDto>('/api/v1/auth/verify', {
      method: 'POST',
      body: { clientDeviceId, challengeId, timestamp, signature },
      authenticated: false,
    });
    return this.unwrap(response);
  }

  hasValidSession(): boolean {
    return this.sessionToken !== null && this.sessionExpiresAt !== null && this.sessionExpiresAt > Date.now();
  }

  clearSession(): void {
    this.sessionToken = null;
    this.sessionExpiresAt = null;
  }

  /** Face-ID-gated challenge-response authentication (public entry point). */
  async authenticate(): Promise<void> {
    await this.options.requireFaceId();
    this.options.onEvent?.('AUTH_STARTED', 'Authentication started');

    const challenge = await this.requestChallenge();
    const timestamp = new Date().toISOString();
    const input = protocol.authenticationSigningInput(
      this.options.deviceId,
      challenge.challenge,
      timestamp,
      protocol.CHALLENGE_VERIFY_ENDPOINT,
    );
    const signature = await this.options.signer.sign(input);
    const session = await this.verifyAndGetSession(
      this.options.deviceId,
      challenge.challengeId,
      timestamp,
      base64urlEncode(signature),
    );

    this.sessionToken = session.sessionToken;
    this.sessionExpiresAt = new Date(session.sessionExpires).getTime();
    this.options.onEvent?.('AUTH_SUCCESS', 'Authentication succeeded');
  }

  private async send<T>(
    path: string,
    opts: SendOptions,
  ): Promise<ApiResponse<T>> {
    if (opts.authenticated && !this.hasValidSession()) {
      await this.authenticate();
    }

    const headers: Record<string, string> = { 'Content-Type': 'application/json' };
    if (opts.authenticated && this.sessionToken) {
      headers.Authorization = `Bearer ${this.sessionToken}`;
    }

    let response;
    try {
      response = await WinlockNetworkingNative.pinnedRequest({
        url: `${this.options.baseUrl}${path}`,
        method: opts.method,
        headers,
        body: opts.body ? JSON.stringify(opts.body) : undefined,
        pin: this.options.pin,
        mode: this.options.mode,
      });
    } catch (error) {
      throw mapNativeError(error);
    }

    // A rejected session token means the token expired or the device was
    // unpaired; re-authenticate once and retry.
    if (response.status === 401 && opts.authenticated && !opts.retried) {
      this.clearSession();
      await this.authenticate();
      return this.send<T>(path, { ...opts, retried: true });
    }

    let parsed: ApiResponse<T>;
    try {
      parsed = JSON.parse(response.body) as ApiResponse<T>;
    } catch {
      throw new InvalidResponseError();
    }
    if (!parsed || typeof parsed.success !== 'boolean') {
      throw new InvalidResponseError();
    }
    return parsed;
  }

  private unwrap<T>(response: ApiResponse<T>): T {
    if (!response.success) {
      throw new ServerError(
        response.error?.message ?? 'Unknown error',
        response.error?.code ?? 'UNKNOWN',
      );
    }
    if (response.data === undefined) {
      throw new InvalidResponseError();
    }
    return response.data;
  }

  private requireSuccess<T>(response: ApiResponse<T>): void {
    if (!response.success) {
      throw new ServerError(
        response.error?.message ?? 'Unknown error',
        response.error?.code ?? 'UNKNOWN',
      );
    }
  }
}

function mapNativeError(error: unknown): Error {
  const code = (error as { code?: string })?.code;
  const message = (error as Error)?.message;
  if (code === 'ERR_CERTIFICATE') {
    return new CertificateError(message);
  }
  if (code === 'ERR_TRANSPORT' || code === 'ERR_INVALID_REQUEST') {
    return new NetworkError(message);
  }
  if (code === 'ERR_AUTH' || /401|expired|invalid.*token/i.test(message ?? '')) {
    return new AuthenticationError(message);
  }
  return new NetworkError(message);
}