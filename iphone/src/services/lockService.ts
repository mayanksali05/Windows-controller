import type { WindowsApiClient } from '../api/windowsApiClient';
import type { FaceIdGate } from '../auth/faceIdGate';

/**
 * Face ID gate for one privileged action: if the session has to be refreshed,
 * the challenge-response login already prompts for Face ID; otherwise prompt
 * explicitly. Either way the user sees exactly one prompt per action.
 */
export async function requireFaceIdForAction(
  client: WindowsApiClient,
  faceId: FaceIdGate,
  reason: string,
): Promise<void> {
  const prompted = await client.ensureSession();
  if (!prompted) {
    await faceId.require(reason);
  }
}

/** Remote lock: Face ID, then the authenticated lock request. */
export async function lockLaptop(
  client: WindowsApiClient,
  faceId: FaceIdGate,
  onEvent?: (kind: string, message: string) => void,
): Promise<void> {
  await requireFaceIdForAction(client, faceId, 'Lock your Windows laptop');
  onEvent?.('LOCK_REQUESTED', 'Lock requested');
  try {
    await client.lock();
    onEvent?.('LOCK_SUCCESS', 'Laptop locked');
  } catch (error) {
    onEvent?.('LOCK_FAILED', error instanceof Error ? error.message : 'Lock failed');
    throw error;
  }
}
