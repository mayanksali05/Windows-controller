import type { WindowsApiClient } from '../api/windowsApiClient';
import type { ProximityStateString } from '../types/protocol';
import { deriveProximity } from '../bluetooth/proximityState';

export interface ProximityChange {
  state: ProximityStateString;
  authenticated: boolean;
}

/**
 * Polls the server-reported proximity (the Windows service scans for the
 * phone) and derives the combined state. BLE presence is a proximity signal
 * only and never authenticates.
 */
export class ProximityMonitor {
  private timer: ReturnType<typeof setInterval> | null = null;
  private listeners = new Set<(change: ProximityChange) => void>();
  private last: ProximityChange = { state: 'UNKNOWN', authenticated: false };

  constructor(
    private readonly client: WindowsApiClient,
    private readonly intervalMs = 5000,
  ) {}

  start(): void {
    if (this.timer) {
      return;
    }
    void this.poll();
    this.timer = setInterval(() => void this.poll(), this.intervalMs);
  }

  stop(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }

  subscribe(listener: (change: ProximityChange) => void): () => void {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  get current(): ProximityChange {
    return this.last;
  }

  private async poll(): Promise<void> {
    // Never prompt for Face ID from a background poll; only read proximity when
    // an authenticated session already exists.
    if (!this.client.hasValidSession()) {
      return;
    }
    try {
      const proximity = await this.client.getProximity();
      const isAuthenticated = this.client.hasValidSession();
      const state = deriveProximity(proximity.state, isAuthenticated);
      const change: ProximityChange = { state, authenticated: isAuthenticated };
      if (change.state !== this.last.state) {
        this.last = change;
        for (const listener of this.listeners) {
          listener(change);
        }
      }
    } catch {
      // Server unreachable: keep last known state.
    }
  }
}