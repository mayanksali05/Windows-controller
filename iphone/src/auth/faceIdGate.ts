import * as LocalAuthentication from 'expo-local-authentication';
import { FaceIdCancelledError, FaceIdError } from '../api/errors';

export interface FaceIdGate {
  /** Prompt for Face ID / passcode. Throws on failure or cancellation. */
  require(reason: string): Promise<void>;
}

/**
 * Face ID gate using expo-local-authentication. Falls back to the device
 * passcode where appropriate. Handles success, failure, cancellation,
 * unavailable hardware/biometrics, and passcode fallback.
 */
export const faceIdGate: FaceIdGate = {
  async require(reason: string): Promise<void> {
    const hasHardware = await LocalAuthentication.hasHardwareAsync();
    const isEnrolled = await LocalAuthentication.isEnrolledAsync();

    if (!hasHardware || !isEnrolled) {
      // Fall back to the device passcode only if that flow is available.
      const result = await LocalAuthentication.authenticateAsync({
        promptMessage: reason,
        cancelLabel: 'Cancel',
        fallbackLabel: 'Use Passcode',
        disableDeviceFallback: false,
      });
      handleResult(result, reason);
      return;
    }

    const result = await LocalAuthentication.authenticateAsync({
      promptMessage: reason,
      cancelLabel: 'Cancel',
      fallbackLabel: 'Use Passcode',
      disableDeviceFallback: false,
    });
    handleResult(result, reason);
  },
};

function handleResult(
  result: LocalAuthentication.LocalAuthenticationResult,
  reason: string,
): void {
  if (result.success) {
    return;
  }
  if (result.error === 'user_cancel') {
    throw new FaceIdCancelledError();
  }
  if (result.error === 'app_cancel' || result.error === 'system_cancel') {
    throw new FaceIdCancelledError();
  }
  throw new FaceIdError(`Authentication failed for "${reason}"`, result.error ?? 'unknown');
}