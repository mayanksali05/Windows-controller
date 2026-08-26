/**
 * Typed errors for the WinLock API client and supporting layers.
 */

export class ApiClientError extends Error {
  constructor(
    message: string,
    readonly code?: string,
  ) {
    super(message);
    this.name = 'ApiClientError';
  }
}

/** Could not reach the laptop (offline, wrong IP, connection refused). */
export class NetworkError extends ApiClientError {
  constructor(message = 'Could not reach the laptop') {
    super(message, 'ERR_NETWORK');
    this.name = 'NetworkError';
  }
}

/** The server certificate did not match the pinned certificate. */
export class CertificateError extends ApiClientError {
  constructor(message = 'The laptop certificate did not match the pinned certificate') {
    super(message, 'ERR_CERTIFICATE');
    this.name = 'CertificateError';
  }
}

export class TimeoutError extends ApiClientError {
  constructor(message = 'The request timed out') {
    super(message, 'ERR_TIMEOUT');
    this.name = 'TimeoutError';
  }
}

/** The server returned a structured error envelope with success:false. */
export class ServerError extends ApiClientError {
  constructor(
    message: string,
    readonly serverCode: string,
  ) {
    super(message, `ERR_SERVER_${serverCode}`);
    this.name = 'ServerError';
  }
}

/** The response was not a well-formed WinLock envelope. */
export class InvalidResponseError extends ApiClientError {
  constructor(message = 'The laptop returned an invalid response') {
    super(message, 'ERR_INVALID_RESPONSE');
    this.name = 'InvalidResponseError';
  }
}

/** The session expired or was rejected and re-authentication failed. */
export class AuthenticationError extends ApiClientError {
  constructor(message = 'Authentication failed') {
    super(message, 'ERR_AUTH');
    this.name = 'AuthenticationError';
  }
}

export class FaceIdError extends ApiClientError {
  constructor(
    message: string,
    readonly reason: string,
  ) {
    super(message, 'ERR_FACE_ID');
    this.name = 'FaceIdError';
  }
}

/** The user cancelled the Face ID / passcode prompt. */
export class FaceIdCancelledError extends FaceIdError {
  constructor() {
    super('Authentication cancelled', 'cancelled');
    this.name = 'FaceIdCancelledError';
  }
}

export class InvalidPairingPayloadError extends ApiClientError {
  constructor(message = 'The pairing payload is invalid') {
    super(message, 'ERR_PAIRING_PAYLOAD');
    this.name = 'InvalidPairingPayloadError';
  }
}