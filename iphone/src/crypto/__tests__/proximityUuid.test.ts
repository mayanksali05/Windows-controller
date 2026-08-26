import { proximityServiceUuid } from '../proximityUuid';

describe('proximityServiceUuid (RFC 4122 v5)', () => {
  it('matches the Windows known-answer vector', () => {
    expect(proximityServiceUuid('PHONE12345678ABCD')).toBe('3a1126f5-eb3b-51c8-9164-9b40a19c7341');
  });

  it('is deterministic', () => {
    expect(proximityServiceUuid('PHONE12345678ABCD')).toBe(proximityServiceUuid('PHONE12345678ABCD'));
  });

  it('differs across devices', () => {
    expect(proximityServiceUuid('PHONE12345678ABCD')).not.toBe(proximityServiceUuid('PHONE12345678ABCE'));
  });

  it('has version 5 and RFC 4122 variant bits', () => {
    const uuid = proximityServiceUuid('PHONE12345678ABCD');
    expect(uuid[14]).toBe('5');
    const variantNibble = uuid[19];
    expect('89ab'.includes(variantNibble)).toBe(true);
  });
});