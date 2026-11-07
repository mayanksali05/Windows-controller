# Windows Unlock

**Status: implemented** using a Windows **Credential Provider**, the supported
way to add a sign-in option to the Windows lock screen. It is in
`windows/WinLock.CredentialProvider` (C++ COM DLL). Nothing about Windows
sign-in is bypassed: the iPhone supplies your **real Windows password**, and
Windows checks it exactly as if you had typed it.

> Not yet run on real hardware. The code compiles (MinGW cross-compile with
> `-Wall -Wextra`, no warnings), and the service side is covered by tests. The
> first end-to-end run must still happen on your laptop: see
> [Testing safely](#5-testing-safely).

## 1. How it works

```
iPhone                                  Laptop (locked)
------                                  ---------------
tap Unlock
Face ID ─────────────┐
read Windows password│(Keychain)
                     ▼
POST /api/v1/unlock ──TLS (pinned) + session token──▶ WinLock.Service (runs as you)
                                                          │ named pipe \\.\pipe\WinLockUnlock
                                                          │ (owner must be SYSTEM)
                                                          ▼
                                       LogonUI ─ WinLock credential provider (SYSTEM)
                                                          │ KERB_INTERACTIVE_UNLOCK_LOGON
                                                          ▼
                                       Windows (LSA) checks the password ─▶ unlocked / rejected
```

1. The phone authenticates with the usual Face ID + Ed25519 challenge-response
   and gets a session (one Face ID prompt per action).
2. Only after Face ID does it read the password from the iPhone Keychain
   (`WHEN_UNLOCKED_THIS_DEVICE_ONLY`: never backed up, never moved to another
   device). It sends the password in the body of `POST /api/v1/unlock` over the
   pinned TLS connection.
3. `WinLock.Service` (`Unlocking/CredentialProviderUnlockService`) connects to
   the provider's named pipe. **It refuses to send anything unless the pipe's
   owner is SYSTEM**, so an ordinary program cannot create a fake pipe to
   capture the password.
4. The credential provider, running in LogonUI, checks that the connecting
   process belongs to **the locked user**. It then marks that user's "Unlock
   with iPhone" sign-in option as the default with auto-submit and asks
   LogonUI to refresh.
5. LogonUI asks for the credential. The provider builds a standard unlock logon
   (a `KERB_INTERACTIVE_UNLOCK_LOGON` for local accounts, or
   `CredPackAuthenticationBuffer` for Microsoft and domain accounts, as in
   Microsoft's V2 sample) and hands it to Windows.
6. Windows reports the result to the provider (`ReportResult`). The result
   goes back through the pipe to the service and then to the phone:
   success, `UNLOCK_PASSWORD_REJECTED`, account locked out, and so on.

Your normal password, PIN, and Windows Hello sign-in stay exactly as they were.
The WinLock option is an extra choice under your user tile.

## 2. Security properties

- **No bypass.** The provider is not an enforcement mechanism. Windows (LSA)
  validates the password. A wrong password fails like a mistyped one and
  counts toward the account lockout policy.
- **The password lives only on the iPhone.** The laptop never stores it. The
  service holds it in memory for one request and zeroes the pipe buffer. The
  provider zeroes its copies after use. The password is never logged: the
  logger drops any field whose name contains "password", and the unlock code
  never passes it to the logger anyway.
- **Who can trigger an unlock:** only a paired phone with a valid session
  (Face ID + signed challenge), at most **5 attempts per minute**. On the
  laptop side, only the locked user's own processes (or SYSTEM) can talk to
  the provider.
- **Where unlock works:** only on the lock screen (`CPUS_UNLOCK_WORKSTATION`).
  The provider does nothing at boot sign-in, in UAC/CredUI prompts, or on
  password-change screens.
- **Stolen-phone risk.** Someone with your unlocked phone **and** your Face ID
  or passcode can unlock the laptop. That is the purpose of the feature.
  Remove the password in the app or unpair the phone (tray → Paired devices)
  if the phone is lost.

## 3. Setup

On the laptop:

1. Get the DLL, either way:
   - **No local install (recommended if C: is short on space):** on GitHub, go
     to **Actions → Windows credential provider → Run workflow**, then download
     the **WinLock-CredentialProvider** artifact and unzip it.
   - **Local build:** Visual Studio (Community or Build Tools, 2019 or newer)
     with **Desktop development with C++**, then
     `.\scripts\build-credential-provider.ps1`.
2. Install from an **elevated** PowerShell:
   `.\scripts\install-credential-provider.ps1 -DllPath <path to the DLL>`
   (leave out `-DllPath` after a local build).
   This copies the DLL to `C:\Windows\System32` and registers it under
   `HKLM\...\Authentication\Credential Providers\{B8C6F5E2-4A5D-4C1B-9E0F-3D2A7C8B9E41}`.
3. Run the WinLock service **as yourself** in your session (for example
   `.\scripts\run-development.ps1`). It must keep running while the laptop is
   locked; it does.

On the iPhone (WinLock app → your laptop):

4. **Unlock password → Set password.** Enter your **Windows account password**,
   not your PIN. For a Microsoft account, this is your Microsoft account
   password.
5. Lock the laptop (Win+L or the Lock button), then tap **Unlock**.

## 4. Requirements and known limitations

- Windows 10 1809+ / Windows 11 (V2 credential providers with user tiles).
- The password must be one Windows accepts for the account. If the setting
  **"For improved security, only allow Windows Hello sign-in for Microsoft
  accounts on this device"** (Settings → Accounts → Sign-in options) is on,
  Windows refuses password sign-in for that account. Turn it off for iPhone
  unlock to work.
- If you change your Windows password, update it in the app. The app shows
  "Windows rejected the password" until you do.
- The service must run in the locked user's session (the development console
  mode does). Unlock does not work if the service is not running.
- On some builds LogonUI only processes the request after the lock-screen
  picture has been dismissed (screen awake). If the app reports "Windows did
  not pick up the unlock request", wake the screen and tap Unlock again.
- Fast User Switching with several users locked at once: only the user running
  the WinLock service can be unlocked.

## 5. Testing safely

The provider is additive and leaves your normal sign-in untouched. Still, it
loads into the lock screen, so:

- **Create a restore point** before the first install.
- Test with **Win+L** while you are at the laptop. Check that your normal
  password/PIN still appears next to the WinLock option.
- To turn it off without uninstalling: `.\scripts\install-credential-provider.ps1 -Disable`.
  To remove it: `-Uninstall`.
- If the lock screen ever misbehaves, sign in with your PIN or password. You
  can also sign in to another administrator account (or boot into Safe Mode
  with Command Prompt) and run:
  ```
  reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers\{B8C6F5E2-4A5D-4C1B-9E0F-3D2A7C8B9E41}" /f
  ```

## 6. Code map

| Piece | File |
|---|---|
| Lock-screen provider (COM, LogonUI) | `windows/WinLock.CredentialProvider/Provider.cpp` |
| Sign-in option + credential serialization | `windows/WinLock.CredentialProvider/Credential.cpp` |
| Pipe server, pending request, result | `windows/WinLock.CredentialProvider/UnlockState.cpp` |
| Pipe wire format | `windows/WinLock.CredentialProvider/common.h` ⇄ `windows/WinLock.Service/Unlocking/UnlockPipeProtocol.cs` |
| Service side (owner check, result mapping) | `windows/WinLock.Service/Unlocking/CredentialProviderUnlockService.cs` |
| Endpoint (`POST /api/v1/unlock`) | `windows/WinLock.Service/Api/UnlockController.cs` |
| iPhone flow | `iphone/src/services/unlockService.ts`, `iphone/src/api/windowsApiClient.ts` |
| iPhone password storage | `iphone/src/storage/laptopStore.ts` (`setUnlockPassword`, …) |

## 7. References

- Microsoft Learn: *Credential providers in Windows*. Providers gather and
  serialize credentials; LSA enforces.
- Microsoft Windows-classic-samples: `CredentialProviders/SampleV2CredentialProvider`
  (serialization helpers this code follows).
- Microsoft Learn: `CREDENTIAL_PROVIDER_USAGE_SCENARIO`, `ICredentialProviderCredential2`,
  `ICredentialProviderSetUserArray`.
