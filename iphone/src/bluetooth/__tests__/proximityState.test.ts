import { deriveProximity } from '../proximityState';

describe('deriveProximity (combined state)', () => {
  it('returns the server state when not authenticated', () => {
    expect(deriveProximity('NEARBY', false)).toBe('NEARBY');
    expect(deriveProximity('AWAY', false)).toBe('AWAY');
    expect(deriveProximity('UNKNOWN', false)).toBe('UNKNOWN');
  });

  it('derives AUTHENTICATED from nearby + a valid session', () => {
    expect(deriveProximity('NEARBY', true)).toBe('AUTHENTICATED');
  });

  it('does not authenticate merely because the device is nearby', () => {
    // BLE proximity must never grant authentication.
    expect(deriveProximity('NEARBY', false)).not.toBe('AUTHENTICATED');
    expect(deriveProximity('AWAY', false)).toBe('AWAY');
    expect(deriveProximity('AWAY', true)).toBe('AWAY');
  });
});