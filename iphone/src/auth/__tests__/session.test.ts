import { isExpired, shouldRefresh } from '../session';

describe('session helpers', () => {
  it('isExpired', () => {
    expect(isExpired(null)).toBe(true);
    expect(isExpired(Date.now() + 1000)).toBe(false);
    expect(isExpired(Date.now() - 1000)).toBe(true);
  });

  it('shouldRefresh', () => {
    expect(shouldRefresh(undefined)).toBe(true);
    expect(shouldRefresh(new Date(Date.now() + 60_000).toISOString())).toBe(false);
    expect(shouldRefresh(new Date(Date.now() - 60_000).toISOString())).toBe(true);
    expect(shouldRefresh('not-a-date')).toBe(true);
  });
});