import type { ProximityStateString } from '../types/protocol';

/**
 * Combined proximity state. The server reports UNKNOWN / NEARBY / AWAY; the
 * spec's AUTHENTICATED state is derived client-side (nearby + a valid
 * authenticated session). BLE presence never authenticates anything.
 */
export function deriveProximity(
  serverState: ProximityStateString,
  isAuthenticated: boolean,
): ProximityStateString {
  if (isAuthenticated && serverState === 'NEARBY') {
    return 'AUTHENTICATED';
  }
  return serverState;
}