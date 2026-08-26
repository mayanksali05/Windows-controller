import type { WindowsApiClient } from '../api/windowsApiClient';
import type { StatusDto, SettingsDto } from '../types/protocol';

export async function fetchStatus(client: WindowsApiClient): Promise<StatusDto> {
  return client.getStatus();
}

export async function fetchSettings(client: WindowsApiClient): Promise<SettingsDto> {
  return client.getSettings();
}