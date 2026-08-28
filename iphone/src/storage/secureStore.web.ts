/**
 * WEB PREVIEW ONLY — not a supported target and NOT secure storage. The real
 * iOS implementation lives in secureStore.ts (expo-secure-store / Keychain).
 * On web there is no Keychain, so this falls back to localStorage so the UI can
 * render in a browser. Never used on iOS; never used to store real secrets.
 */
export const SECURE_KEYS = {
  identitySeed: 'winlock.identity.seed',
  windowsPublicKey(deviceId: string): string {
    return `winlock.laptop.${deviceId}.publicKey`;
  },
  tlsPin(deviceId: string): string {
    return `winlock.laptop.${deviceId}.tlsPin`;
  },
} as const;

const PREFIX = 'winlock.web.secure.';

export async function secureSet(key: string, value: string): Promise<void> {
  // eslint-disable-next-line no-console
  console.warn(`[web preview] secureSet('${key}') uses localStorage — NOT secure. iOS uses the Keychain.`);
  localStorage.setItem(PREFIX + key, value);
}

export async function secureGet(key: string): Promise<string | null> {
  return localStorage.getItem(PREFIX + key);
}

export async function secureDelete(key: string): Promise<void> {
  localStorage.removeItem(PREFIX + key);
}