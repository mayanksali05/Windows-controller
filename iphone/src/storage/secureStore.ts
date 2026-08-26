import * as SecureStore from 'expo-secure-store';

/**
 * Secure key-value storage backed by the iOS Keychain (expo-secure-store).
 * Used for the iPhone identity private key, Windows public keys, and TLS pins.
 * Never used for non-secret metadata and never for logging.
 */

const ACCESSIBLE: SecureStore.KeychainAccessibilityConstant =
  SecureStore.AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY;

export const SECURE_KEYS = {
  identitySeed: 'winlock.identity.seed',
  windowsPublicKey(deviceId: string): string {
    return `winlock.laptop.${deviceId}.publicKey`;
  },
  tlsPin(deviceId: string): string {
    return `winlock.laptop.${deviceId}.tlsPin`;
  },
} as const;

export async function secureSet(key: string, value: string): Promise<void> {
  await SecureStore.setItemAsync(key, value, { keychainAccessible: ACCESSIBLE });
}

export async function secureGet(key: string): Promise<string | null> {
  return SecureStore.getItemAsync(key);
}

export async function secureDelete(key: string): Promise<void> {
  await SecureStore.deleteItemAsync(key);
}