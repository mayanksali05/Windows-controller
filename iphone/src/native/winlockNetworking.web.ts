/**
 * WEB PREVIEW ONLY — not a supported target. React Native's real iOS
 * implementation lives in winlockNetworking.ts (native module). On web there is
 * no Bonjour or pinned-TLS transport, so all networking is inert and requests
 * fail loudly. Never used on iOS.
 */
import type { DiscoveredLaptop } from '../types/protocol';
import type { PinnedRequestOptions, PinnedResponse } from './winlockNetworking';

/**
 * WEB PREVIEW ONLY — not a supported target. On web there is no Bonjour or
 * pinned-TLS transport. Discovery is a silent no-op (nothing is found) and any
 * real request fails loudly. Never used on iOS.
 */
export const WinlockNetworkingNative = {
  pinnedRequest: async (_options: PinnedRequestOptions): Promise<PinnedResponse> => {
    throw new Error('Pinned networking is unavailable in the web preview');
  },
  startDiscovery: (): void => {},
  stopDiscovery: (): void => {},
};

export function onLaptopDiscovered(_listener: (laptop: DiscoveredLaptop) => void): () => void {
  return () => {};
}