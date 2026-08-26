import type { WindowsApiClient } from '../api/windowsApiClient';
import type { FaceIdGate } from '../auth/faceIdGate';

/**
 * Remote lock: Face ID gates the privileged action, then the client (which
 * Face-ID-gates the challenge signing) issues the authenticated lock request.
 */
export async function lockLaptop(
  client: WindowsApiClient,
  faceId: FaceIdGate,
  onEvent?: (kind: string, message: string) => void,
): Promise<void> {
  await faceId.require('Lock your Windows laptop');
  onEvent?.('LOCK_REQUESTED', 'Lock requested');
  try {
    await client.lock();
    onEvent?.('LOCK_SUCCESS', 'Laptop locked');
  } catch (error) {
    onEvent?.('LOCK_FAILED', error instanceof Error ? error.message : 'Lock failed');
    throw error;
  }
}