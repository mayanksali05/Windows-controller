import type { WindowsApiClient } from '../api/windowsApiClient';
import type { DeviceIdentity } from '../crypto/identity';

/**
 * High-level authentication facade: guarantees a valid session for privileged
 * operations. The Face-ID gate runs inside the client's challenge-response
 * flow; this service just ensures the session is fresh and exposes the derived
 * "authenticated" flag used to compute proximity state.
 */
export class AuthenticationService {
  private _authenticated = false;

  constructor(
    private readonly client: WindowsApiClient,
    private readonly identity: DeviceIdentity,
  ) {}

  get isAuthenticated(): boolean {
    return this._authenticated;
  }

  get deviceId(): string {
    return this.identity.deviceId;
  }

  /** Ensure a valid session exists (no-op when already valid). */
  async ensureAuthenticated(): Promise<void> {
    if (this._authenticated && this.client.hasValidSession()) {
      return;
    }
    await this.client.authenticate();
    this._authenticated = true;
  }

  /** Explicitly re-authenticate (used before privileged actions). */
  async authenticateForPrivilegedAction(): Promise<void> {
    await this.ensureAuthenticated();
  }

  markAuthenticated(): void {
    this._authenticated = true;
  }

  markUnauthenticated(): void {
    this._authenticated = false;
    this.client.clearSession();
  }
}