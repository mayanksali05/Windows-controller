import { unlockLaptop } from '../unlockService';
import { lockLaptop } from '../lockService';
import { FaceIdCancelledError, UnlockPasswordMissingError } from '../../api/errors';
import type { WindowsApiClient } from '../../api/windowsApiClient';
import type { FaceIdGate } from '../../auth/faceIdGate';
import {
  clearUnlockPassword,
  getUnlockPassword,
  hasUnlockPassword,
  removeLaptop,
  setUnlockPassword,
} from '../../storage/laptopStore';

function fakeClient(sessionNeedsRefresh: boolean) {
  return {
    ensureSession: jest.fn(async () => sessionNeedsRefresh),
    unlock: jest.fn(async (_password: string) => {}),
    lock: jest.fn(async () => {}),
  };
}

function fakeFaceId(): FaceIdGate & { require: jest.Mock } {
  return { require: jest.fn(async (_reason: string) => {}) };
}

const asClient = (c: ReturnType<typeof fakeClient>) => c as unknown as WindowsApiClient;

describe('unlockLaptop', () => {
  it('sends the saved password after one explicit Face ID prompt when the session is valid', async () => {
    const client = fakeClient(false);
    const faceId = fakeFaceId();
    const events: string[] = [];

    await unlockLaptop(asClient(client), 'LAPTOP1', faceId, async () => 'pw', (kind) => events.push(kind));

    expect(faceId.require).toHaveBeenCalledTimes(1);
    expect(client.unlock).toHaveBeenCalledWith('pw');
    expect(events).toEqual(['UNLOCK_REQUESTED', 'UNLOCK_SUCCESS']);
  });

  it('does not prompt twice when logging in already used Face ID', async () => {
    const client = fakeClient(true);
    const faceId = fakeFaceId();

    await unlockLaptop(asClient(client), 'LAPTOP1', faceId, async () => 'pw');

    expect(faceId.require).not.toHaveBeenCalled();
    expect(client.unlock).toHaveBeenCalledTimes(1);
  });

  it('reads the password only after Face ID succeeds', async () => {
    const client = fakeClient(false);
    const faceId = fakeFaceId();
    faceId.require.mockRejectedValueOnce(new FaceIdCancelledError());
    const loadPassword = jest.fn(async () => 'pw');

    await expect(unlockLaptop(asClient(client), 'LAPTOP1', faceId, loadPassword)).rejects.toBeInstanceOf(
      FaceIdCancelledError,
    );
    expect(loadPassword).not.toHaveBeenCalled();
    expect(client.unlock).not.toHaveBeenCalled();
  });

  it('fails clearly when no password is saved', async () => {
    const client = fakeClient(false);

    await expect(
      unlockLaptop(asClient(client), 'LAPTOP1', fakeFaceId(), async () => null),
    ).rejects.toBeInstanceOf(UnlockPasswordMissingError);
    expect(client.unlock).not.toHaveBeenCalled();
  });

  it('never puts the password in log events', async () => {
    const client = fakeClient(false);
    client.unlock.mockRejectedValueOnce(new Error('Windows rejected the password.'));
    const messages: string[] = [];

    await expect(
      unlockLaptop(asClient(client), 'LAPTOP1', fakeFaceId(), async () => 'hunter2', (_k, m) => messages.push(m)),
    ).rejects.toThrow();
    expect(messages.join(' ')).not.toContain('hunter2');
  });
});

describe('lockLaptop', () => {
  it('prompts for Face ID once', async () => {
    const valid = fakeClient(false);
    const faceId = fakeFaceId();
    await lockLaptop(asClient(valid), faceId);
    expect(faceId.require).toHaveBeenCalledTimes(1);

    const refreshed = fakeClient(true);
    const faceId2 = fakeFaceId();
    await lockLaptop(asClient(refreshed), faceId2);
    expect(faceId2.require).not.toHaveBeenCalled();
    expect(refreshed.lock).toHaveBeenCalledTimes(1);
  });
});

describe('unlock password storage', () => {
  it('stores per laptop in the Keychain and is removed on unpair', async () => {
    expect(await hasUnlockPassword('L1')).toBe(false);
    await setUnlockPassword('L1', 'pw1');
    await setUnlockPassword('L2', 'pw2');
    expect(await getUnlockPassword('L1')).toBe('pw1');

    await removeLaptop('L1');
    expect(await hasUnlockPassword('L1')).toBe(false);
    expect(await getUnlockPassword('L2')).toBe('pw2');

    await clearUnlockPassword('L2');
    expect(await hasUnlockPassword('L2')).toBe(false);
  });
});
