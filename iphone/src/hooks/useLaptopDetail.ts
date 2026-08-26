import { useCallback, useEffect, useState } from 'react';
import type { WindowsApiClient } from '../api/windowsApiClient';
import type { SettingsDto, StatusDto } from '../types/protocol';
import { lockLaptop } from '../services/lockService';
import { useServices } from '../services/servicesContext';
import { faceIdGate } from '../auth/faceIdGate';
import { FaceIdCancelledError } from '../api/errors';

export interface LaptopDetailState {
  status: StatusDto | null;
  settings: SettingsDto | null;
  error: string | null;
  isLocking: boolean;
  authenticated: boolean;
  refresh: () => Promise<void>;
  lock: () => Promise<void>;
}

export function useLaptopDetail(client: WindowsApiClient | null): LaptopDetailState {
  const services = useServices();
  const [status, setStatus] = useState<StatusDto | null>(null);
  const [settings, setSettings] = useState<SettingsDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLocking, setIsLocking] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);

  const refresh = useCallback(async () => {
    if (!client) {
      return;
    }
    try {
      const [s, st] = await Promise.all([client.getStatus(), client.getSettings()]);
      setStatus(s);
      setSettings(st);
      setAuthenticated(client.hasValidSession());
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Could not reach the laptop');
    }
  }, [client]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const lock = useCallback(async () => {
    if (!client) {
      return;
    }
    setIsLocking(true);
    try {
      await lockLaptop(client, faceIdGate, (kind, message) => services.logStore.add(kind, message));
      setAuthenticated(client.hasValidSession());
    } catch (e) {
      if (!(e instanceof FaceIdCancelledError)) {
        setError(e instanceof Error ? e.message : 'Lock failed');
      }
    } finally {
      setIsLocking(false);
      void refresh();
    }
  }, [client, services, refresh]);

  return { status, settings, error, isLocking, authenticated, refresh, lock };
}