/**
 * Protocol types mirroring the Windows WinLock.Protocol models.
 * The wire format is camelCase JSON (ASP.NET Core defaults).
 */

export const ErrorCodes = {
  AUTH_FAILED: 'AUTH_FAILED',
  CHALLENGE_EXPIRED: 'CHALLENGE_EXPIRED',
  CHALLENGE_REPLAYED: 'CHALLENGE_REPLAYED',
  DEVICE_UNKNOWN: 'DEVICE_UNKNOWN',
  DEVICE_UNAUTHORIZED: 'DEVICE_UNAUTHORIZED',
  PAIRING_INVALID: 'PAIRING_INVALID',
  PAIRING_EXPIRED: 'PAIRING_EXPIRED',
  LOCK_FAILED: 'LOCK_FAILED',
  RATE_LIMITED: 'RATE_LIMITED',
  MALFORMED_REQUEST: 'MALFORMED_REQUEST',
  INTERNAL_ERROR: 'INTERNAL_ERROR',
} as const;

export type ErrorCode = (typeof ErrorCodes)[keyof typeof ErrorCodes];

export interface ApiErrorBody {
  code: string;
  message: string;
}

/** Standard response envelope used by every WinLock endpoint. */
export interface ApiResponse<T = undefined> {
  success: boolean;
  message?: string;
  error?: ApiErrorBody;
  data?: T;
}

export type ProximityStateString = 'UNKNOWN' | 'NEARBY' | 'AWAY' | 'AUTHENTICATED';
export type SecurityStateString = 'PAIRED' | 'NOT_PAIRED';

/** GET /api/v1/status */
export interface StatusDto {
  isLocked: boolean | null;
  batteryPercent: number | null;
  serviceVersion: string;
  environment: string;
  lockAvailable: boolean;
  proximity: ProximityStateString;
  security: SecurityStateString;
}

/** GET /api/v1/settings */
export interface SettingsDto {
  proximityEnabled: boolean;
  proximityAwayTimeoutSeconds: number;
  proximityNearbyRssiThreshold: number;
  automaticLockEnabled: boolean;
  autoLockAwayDurationSeconds: number;
}

/** POST /api/v1/pair/request response data */
export interface PairingInfoDto {
  deviceId: string;
  windowsPublicKey: string;
  pairingAvailable: boolean;
  pairingNonce?: string;
  expiresAt?: string;
  signature?: string;
}

/** The pairing payload shown as a QR code (also returned by /pair/session). */
export interface PairingSessionPayload {
  version: number;
  deviceId: string;
  windowsPublicKey: string;
  pairingNonce: string;
  pairingToken: string;
  expiresAt: string;
  signature: string;
  tlsPin: string;
}

/** POST /api/v1/auth/challenge response data */
export interface AuthChallengeDto {
  challengeId: string;
  challenge: string;
  expiresAt: string;
}

/** POST /api/v1/auth/verify response data */
export interface AuthVerifyResponseDto {
  sessionToken: string;
  sessionExpires: string;
  proximity: string;
}

/** GET /api/v1/proximity response data */
export interface ProximityDto {
  state: ProximityStateString;
  deviceId?: string | null;
  rssi?: number | null;
  updatedAt: string;
}

/** GET /api/v1/pair/devices entry */
export interface AuthorizedDeviceDto {
  deviceId: string;
  name: string;
  pairedAt: string;
}

/** Paired laptop metadata stored on the device (non-secret). */
export interface PairedLaptop {
  deviceId: string;
  name: string;
  host: string;
  port: number;
  pairedAt: string;
}

/** A laptop discovered on the LAN via Bonjour. */
export interface DiscoveredLaptop {
  name: string;
  deviceId: string;
  host: string;
  port: number;
}