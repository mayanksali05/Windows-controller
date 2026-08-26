import AsyncStorage from '@react-native-async-storage/async-storage';
import type { PairedLaptop } from '../types/protocol';
import { secureGet, secureSet, secureDelete, SECURE_KEYS } from './secureStore';

/**
 * Non-secret paired-laptop metadata (device id, host, port, name). Keys and
 * pins live in expo-secure-store; never put secrets here.
 */

const LAPTOPS_KEY = 'winlock.laptops';

export async function loadLaptops(): Promise<PairedLaptop[]> {
  const raw = await AsyncStorage.getItem(LAPTOPS_KEY);
  if (!raw) {
    return [];
  }
  try {
    const parsed: PairedLaptop[] = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

async function persist(laptops: PairedLaptop[]): Promise<void> {
  await AsyncStorage.setItem(LAPTOPS_KEY, JSON.stringify(laptops));
}

export async function addLaptop(laptop: PairedLaptop): Promise<void> {
  const laptops = await loadLaptops();
  const index = laptops.findIndex((l) => l.deviceId === laptop.deviceId);
  if (index >= 0) {
    laptops[index] = laptop;
  } else {
    laptops.push(laptop);
  }
  await persist(laptops);
}

export async function removeLaptop(deviceId: string): Promise<void> {
  const laptops = await loadLaptops();
  const next = laptops.filter((l) => l.deviceId !== deviceId);
  await persist(next);
  await secureDelete(SECURE_KEYS.windowsPublicKey(deviceId));
  await secureDelete(SECURE_KEYS.tlsPin(deviceId));
}

export async function findLaptop(deviceId: string): Promise<PairedLaptop | null> {
  const laptops = await loadLaptops();
  return laptops.find((l) => l.deviceId === deviceId) ?? null;
}

/** Persist a paired laptop together with its secure material. */
export async function storePairedLaptop(
  laptop: PairedLaptop,
  windowsPublicKey: string,
  tlsPin: string,
): Promise<void> {
  await addLaptop(laptop);
  await secureSet(SECURE_KEYS.windowsPublicKey(laptop.deviceId), windowsPublicKey);
  await secureSet(SECURE_KEYS.tlsPin(laptop.deviceId), tlsPin);
}

export async function getWindowsPublicKey(deviceId: string): Promise<string | null> {
  return secureGet(SECURE_KEYS.windowsPublicKey(deviceId));
}

export async function getTlsPin(deviceId: string): Promise<string | null> {
  return secureGet(SECURE_KEYS.tlsPin(deviceId));
}