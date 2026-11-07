import { useCallback, useEffect, useState } from 'react';
import type { WindowsApiClient } from '../api/windowsApiClient';
import type { SettingsDto, StatusDto } from '../types/protocol';
import { lockLaptop } from '../services/lockService';
import { unlockLaptop } from '../services/unlockService';
import { useServices } from '../services/servicesContext';
import { faceIdGate } from '../auth/faceIdGate';
import { FaceIdCancelledError } from '../api/errors';
import {
  clearUnlockPassword,
  getUnlockPassword,
  hasUnlockPassword,
  setUnlockPassword,
} from '../storage/laptopStore';

export interface LaptopDetailState {
  status: StatusDto | null;
  settings: SettingsDto | null;
  error: string | null;
  notice: string | null;
  isLocking: boolean;
  isUnlocking: boolean;
  authenticated: boolean;
  hasPassword: boolean;
  refresh: () => Promise<void>;
  lock: () => Promise<void>;
  unlock: () => Promise<void>;
  savePassword: (password: string) => Promise<void>;
  removePassword: () => Promise<void>;
}

export function useLaptopDetail(client: WindowsApiClient | null, laptopDeviceId: string | null): LaptopDetailState {
  const services = useServices();
  const [status, setStatus] = useState<StatusDto | null>(null);
  const [settings, setSettings] = useState<SettingsDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [isLocking, setIsLocking] = useState(false);
  const [isUnlocking, setIsUnlocking] = useState(false);
  const [authenticated, setAuthenticated] = useState(false);
  const [hasPassword, setHasPassword] = useState(false);

  const log = useCallback(
    (kind: string, message: string) => services.logStore.add(kind, message),
    [services],
  );

  useEffect(() => {
    if (!laptopDeviceId) {
      return;
    }
    void hasUnlockPassword(laptopDeviceId).then(setHasPassword);
  }, [laptopDeviceId]);

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
    setNotice(null);
    try {
      await lockLaptop(client, faceIdGate, log);
      setAuthenticated(client.hasValidSession());
      setNotice('Laptop locked');
    } catch (e) {
      if (!(e instanceof FaceIdCancelledError)) {
        setError(e instanceof Error ? e.message : 'Lock failed');
      }
    } finally {
      setIsLocking(false);
      void refresh();
    }
  }, [client, log, refresh]);

  const unlock = useCallback(async () => {
    if (!client || !laptopDeviceId) {
      return;
    }
    setIsUnlocking(true);
    setNotice(null);
    try {
      await unlockLaptop(client, laptopDeviceId, faceIdGate, getUnlockPassword, log);
      setAuthenticated(client.hasValidSession());
      setError(null);
      setNotice('Laptop unlocked');
    } catch (e) {
      if (!(e instanceof FaceIdCancelledError)) {
        setError(e instanceof Error ? e.message : 'Unlock failed');
      }
    } finally {
      setIsUnlocking(false);
      void refresh();
    }
  }, [client, laptopDeviceId, log, refresh]);

  const savePassword = useCallback(
    async (password: string) => {
      if (!laptopDeviceId || !password) {
        return;
      }
      await setUnlockPassword(laptopDeviceId, password);
      setHasPassword(true);
      log('UNLOCK_PASSWORD_SAVED', 'Unlock password saved to the Keychain');
    },
    [laptopDeviceId, log],
  );

  const removePassword = useCallback(async () => {
    if (!laptopDeviceId) {
      return;
    }
    await clearUnlockPassword(laptopDeviceId);
    setHasPassword(false);
    log('UNLOCK_PASSWORD_REMOVED', 'Unlock password removed');
  }, [laptopDeviceId, log]);

  return {
    status,
    settings,
    error,
    notice,
    isLocking,
    isUnlocking,
    authenticated,
    hasPassword,
    refresh,
    lock,
    unlock,
    savePassword,
    removePassword,
  };
}
