import { onLaptopDiscovered, WinlockNetworkingNative } from '../native/winlockNetworking';
import type { DiscoveredLaptop } from '../types/protocol';

export interface DiscoveryService {
  /** Start browsing `_mywinlock._tcp`; returns an unsubscribe function. */
  start(onLaptop: (laptop: DiscoveredLaptop) => void): () => void;
  stop(): void;
}

export const bonjourService: DiscoveryService = {
  start(onLaptop) {
    const unsubscribe = onLaptopDiscovered(onLaptop);
    WinlockNetworkingNative.startDiscovery();
    return unsubscribe;
  },
  stop() {
    WinlockNetworkingNative.stopDiscovery();
  },
};