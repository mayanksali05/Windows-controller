# Windows Unlock — Research and Extension Point

**Status: research complete — unlock is a documented extension point. No
Windows-authentication bypass is implemented, and no fake unlock endpoint
exists.**

## 1. The Problem

Locking a Windows session is supported and implemented (`LockWorkStation`).
*Unlocking* a locked session is fundamentally different: the interactive
session's desktop is replaced by Winlogon, and gaining access requires the user
to authenticate through the OS credential stack. This document summarizes the
supported mechanisms and why a companion .NET service cannot safely unlock a
session on behalf of an iPhone.

## 2. Candidate Mechanisms

### 2.1 Windows Credential Provider (V1/V2/V3)

- The **only supported** way to add third-party credentials to the logon
  screen. Implemented as a COM DLL exposing `ICredentialProvider` /
  `ICredentialProviderCredential` (V2 adds `ICredentialProviderCredential2`).
- Key property (from Microsoft docs): *"credential providers are not
  enforcement mechanisms. They are used to gather and serialize credentials,
  submitting them for authorization. The local authority and authentication
  packages will handle ... security enforcement."*
- Consequence: a credential provider cannot make Winlogon accept a logon it
  would otherwise reject. The iPhone could, at best, drive a tile that still
  requires the user's **real** Windows Hello gesture, PIN, or password on the
  laptop. Fabricating a `KERB_INTERACTIVE_LOGON` or Hello credential from an
  external signature would be a security bypass, not a supported feature.
- Practical barriers: C++ COM, admin install, must coexist with system
  providers, Winlogon/LSA edge cases. Not implementable from managed .NET.

### 2.2 Windows Hello / Windows Hello for Business

- Hello unlocks via biometric/PIN through the credential stack. Credentials are
  tied to the device (TPM) and the user; biometric data never leaves the device.
- **There is no public API for an external device to trigger or validate a Hello
  unlock.** Hello is a local gesture; a remote iPhone cannot act as the Hello
  sensor.

### 2.3 FIDO2 / WebAuthn / Passkeys

- Windows Hello supports FIDO2/WebAuthn security keys and passkeys for
  *website/app sign-in* and, in supported configurations, for local sign-in via
  a registered security key.
- An iPhone could in principle be a FIDO2 authenticator (CTAP2 over
  NFC/BLE/USB), but only if it is **enrolled as a Windows Hello security key**
  for the Windows account — an OS-level, user-interactive enrollment that must
  be performed on the device, with the OS owning the credential. Wiring this
  into a companion service is not a supported integration.

### 2.4 Other APIs — rejected

- `WTSLogoffSession` / `WTSDisconnectSession`: session control, not unlock.
- `LogonUser` / `CreateProcessAsUser`: require credentials or operate within an
  existing session; cannot unlock the Winlogon desktop.
- Auto-logon registry/policy or keyboard injection: **prohibited** (bypass).

## 3. Conclusion

There is **no supported Windows API** that lets an external iPhone unlock a
locked session without the user's real credentials, short of:
- a Credential Provider that still requires the user's Hello gesture/PIN/password
  on the laptop, or
- enrolling the iPhone as a Windows Hello FIDO2/passkey security key.

Neither is implementable as a safe companion-service feature today. Therefore:

- The unlock component is represented by `IWindowsAuthenticationProvider`
  (a clean boundary) with **no implementation** and **no `/unlock` endpoint**.
- Any future implementation must go through a supported OS mechanism and must
  never bypass Winlogon, fabricate credentials, inject input, or weaken Windows
  authentication.
- This is a deliberate, documented limitation — not a hidden shortcut.

## 4. References

- Microsoft Learn: *Credential Providers in Windows* (Win32, SecAuthN) —
  confirms credential providers are the only logon integration point and are
  not enforcement mechanisms.
- Microsoft Learn: *Windows Hello for Business overview* — Hello is a local
  gesture; FIDO2/WebAuthn/passkeys apply through Hello enrollment.
- Microsoft Learn: *CREDENTIAL_PROVIDER_USAGE_SCENARIO*.