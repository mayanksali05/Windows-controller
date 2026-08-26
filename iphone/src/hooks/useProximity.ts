import { useEffect, useState } from 'react';
import type { WindowsApiClient } from '../api/windowsApiClient';
import { ProximityMonitor } from '../services/proximityMonitor';
import type { ProximityStateString } from '../types/protocol';

/** Live combined proximity for a laptop (server-reported + auth derived). */
export function useProximity(client: WindowsApiClient | null): ProximityStateString {
  const [state, setState] = useState<ProximityStateString>('UNKNOWN');

  useEffect(() => {
    if (!client) {
      return;
    }
    const monitor = new ProximityMonitor(client);
    const unsubscribe = monitor.subscribe((change) => setState(change.state));
    monitor.start();
    return () => {
      unsubscribe();
      monitor.stop();
    };
  }, [client]);

  return state;
}