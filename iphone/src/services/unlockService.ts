import type { WindowsApiClient } from '../api/windowsApiClient';
import type { FaceIdGate } from '../auth/faceIdGate';
import { UnlockPasswordMissingError } from '../api/errors';
import { requireFaceIdForAction } from './lockService';

/**
 * Remote unlock: Face ID first, then the saved Windows password is read from
 * the Keychain and sent over the pinned, authenticated session. The laptop's
 * credential provider hands it to Windows, which checks it like a typed one.
 */
export async function unlockLaptop(
  client: WindowsApiClient,
  laptopDeviceId: string,
  faceId: FaceIdGate,
  loadPassword: (laptopDeviceId: string) => Promise<string | null>,
  onEvent?: (kind: string, message: string) => void,
): Promise<void> {
  await requireFaceIdForAction(client, faceId, 'Unlock your Windows laptop');

  const password = await loadPassword(laptopDeviceId);
  if (!password) {
    throw new UnlockPasswordMissingError();
  }

  onEvent?.('UNLOCK_REQUESTED', 'Unlock requested');
  try {
    await client.unlock(password);
    onEvent?.('UNLOCK_SUCCESS', 'Laptop unlocked');
  } catch (error) {
    // Never include the password in events; the message comes from the server.
    onEvent?.('UNLOCK_FAILED', error instanceof Error ? error.message : 'Unlock failed');
    throw error;
  }
}
