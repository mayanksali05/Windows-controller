import { requireNativeModule, EventEmitter } from 'expo-modules-core';
import type { DiscoveredLaptop } from '../types/protocol';

export interface PinnedRequestOptions {
  url: string;
  method: string;
  headers?: Record<string, string>;
  body?: string;
  pin: string;
  mode: 'development' | 'production';
}

export interface PinnedResponse {
  status: number;
  body: string;
}

interface WinlockNetworkingNativeModule {
  pinnedRequest(options: PinnedRequestOptions): Promise<PinnedResponse>;
  startDiscovery(): void;
  stopDiscovery(): void;
}

export type WinlockNetworkingEvents = Record<
  'onLaptopDiscovered',
  (laptop: DiscoveredLaptop) => void
> &
  Record<string, (...args: any[]) => void>;

export const WinlockNetworkingNative =
  requireNativeModule<WinlockNetworkingNativeModule>('WinlockNetworking');

const events = new EventEmitter<WinlockNetworkingEvents>();

/** Subscribe to discovered WinLock laptops. Returns an unsubscribe function. */
export function onLaptopDiscovered(listener: (laptop: DiscoveredLaptop) => void): () => void {
  const subscription = events.addListener('onLaptopDiscovered', listener);
  return () => subscription.remove();
}