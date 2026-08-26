# WinLock — iPhone-based Windows Lock & Secure Unlock System

A secure system for controlling a Windows laptop from an iPhone over the local
network: remote **lock**, **BLE proximity** detection, **Face-ID-backed
cryptographic authentication**, and configurable **automatic locking**. Unlock
is deliberately **not** implemented through any bypass; it is a documented
extension point using supported Windows mechanisms only.

> Security-first: no plain HTTP, no hardcoded secrets, no password handling, no
> Windows-auth bypass. See `docs/security.md` and `docs/threat-model.md`.

## 1. Overview

Two components:

- **`windows/` — WinLock.Service** (.NET 8, ASP.NET Core/Kestrel) — authenticated
  local HTTPS API, secure pairing, challenge-response auth, `LockWorkStation`
  locking, BLE proximity scanning, automatic lock, structured security logging,
  plus a **WinLock.Tray** app.
- **`iphone/` — Expo / React Native (TypeScript) client** — Bonjour discovery,
  QR pairing, Face ID, challenge-response auth, status, lock button, proximity,
  settings, logs (legacy SwiftUI client preserved in `iphone/WinLock/`).

## 2. Architecture

See `docs/architecture.md`. High level:

```
iPhone ── HTTPS/TLS (authenticated) ──> Windows Companion Service ──> Windows OS
iPhone ── BLE advertisement ──────────> Windows (proximity only)
```

Trust = public keys, never IP/MAC/Wi-Fi presence. BLE is proximity-only.

## 3. Requirements

- Windows 10/11 64-bit, .NET SDK 8+.
- macOS with Xcode 15+/iOS 17 SDK for the iPhone app.
- Wi-Fi + Bluetooth on both devices.

## 4. Windows Setup

See `docs/setup.md`.

```powershell
.\scripts\setup-windows.ps1
.\scripts\build-windows.ps1
.\scripts\run-development.ps1
```

## 5. iPhone Setup

See `docs/setup.md` §2 and `iphone/README.md`. The client is an **Expo SDK 54 /
React Native (TypeScript)** app in `iphone/` using development builds (two local
native modules provide TLS-pinned HTTPS + Bonjour discovery, and CoreBluetooth
BLE advertising). The legacy SwiftUI client is preserved under `iphone/WinLock/`
for comparison.

```bash
cd iphone
npm install
eas build --platform ios --profile development
```

Required capabilities: Local Network, Camera (QR pairing), Face ID (via
`expo-local-authentication`), Keychain (`expo-secure-store`), and Bluetooth
Always (BLE advertising). Run on a real device.

## 6. Development Setup

```powershell
.\scripts\setup-windows.ps1   # config, dev cert, firewall
.\scripts\run-tests.ps1
```

## 7. Pairing Procedure

See `docs/setup.md` §3 and `docs/protocol.md` §2. Scan the Windows QR with the
iPhone; public keys are exchanged; the Windows cert public key is pinned.

## 8. Security Model

- Curve25519/Ed25519 public-key cryptography; DPAPI on Windows, Keychain on iOS.
- Challenge-response auth with one-time nonces, replay protection, session
  tokens.
- Face ID gates signing of privileged requests.
- TLS with certificate pinning; development mode is explicit and still pinned.
- Full detail: `docs/security.md`, `docs/threat-model.md`.

## 9. API Documentation

See `docs/protocol.md`. Summary:

| Method | Route            | Auth |
|--------|------------------|------|
| GET    | `/api/v1/status` | session |
| POST   | `/api/v1/pair/request` | anonymous (public identity) |
| POST   | `/api/v1/pair/session` | session |
| POST   | `/api/v1/pair/confirm` | one-time token + sig |
| GET    | `/api/v1/pair/devices` | session |
| POST   | `/api/v1/unpair` | session |
| POST   | `/api/v1/auth/challenge` | anonymous (paired device) |
| POST   | `/api/v1/auth/verify` | single-use challenge + sig |
| POST   | `/api/v1/lock` | session |
| GET    | `/api/v1/proximity` | session |
| GET    | `/api/v1/settings` | session |

## 10. BLE Requirements

- The iPhone advertises a per-device BLE service UUID (RFC 4122 v5 of its
  device id); the Windows service scans for paired devices via the WinRT
  Bluetooth LE advertisement API.
- Proximity states: `UNKNOWN`, `NEARBY`, `AWAY` (`AUTHENTICATED` is derived
  client-side from nearby + an authenticated session).
- Proximity is **not** authentication — privileged operations always require
  Face ID + signed challenge-response.
- Requires Windows 10 build 19041+ with Bluetooth; if unavailable, proximity is
  `UNKNOWN`.

## 10.1 Automatic Locking

- Optional: when enabled, the laptop locks after the paired phone's proximity
  leaves `NEARBY` for a configurable duration (`Security:AutoLockAwayDurationSeconds`,
  default 60s). Brief BLE losses are absorbed by the scanner away timeout, so a
  single dropped scan never locks.
- Locks at most once per absence; skipped if already locked or not in an
  interactive session; only armed when a device is paired.
- Configured on the Windows side (`GET /api/v1/settings` shows the policy to the
  iPhone).

## 11. Testing

Windows/.NET:

```powershell
.\scripts\run-tests.ps1
```

Security tests: invalid signatures, expired/replayed challenges, unknown
devices, malformed requests, unauthorized lock, pairing failures.
API tests: auth success/failure, lock, status, pair, unpair.
Windows tests: lock invocation, service lifecycle, config, secure storage.

Expo client:

```bash
cd iphone
npx jest            # 36 unit tests (protocol, auth, pairing, crypto)
npx tsc --noEmit    # typecheck
```

## 12. Troubleshooting

See `docs/setup.md` §6.

## 13. Known Limitations

- No public-Internet deployment; LAN-only by design.
- iOS build requires a Mac.
- Fully compromised hosts can read DPAPI-protected data (OS boundary).

## 14. Windows Unlock Limitations

Unlocking a locked session requires OS-supported integration. Research
(`docs/windows-unlock.md`) concluded:

- A **Windows Credential Provider** (C++ COM) is the only supported logon
  integration point, but credential providers are not enforcement mechanisms —
  the user's real Windows Hello gesture, PIN, or password is still required on
  the laptop, so an iPhone could at most trigger the flow, never replace the
  credential.
- **Windows Hello / FIDO2 / passkeys** require the iPhone to be enrolled as a
  Windows Hello security key — a deep, user-interactive OS enrollment outside a
  companion service's scope.

Unlock is therefore a **documented extension point** represented by
`IWindowsAuthenticationProvider` with **no implementation and no `/unlock`
endpoint**. No bypass, credential fabrication, keyboard injection, or Windows
authentication weakening is shipped.

## Repository Layout

```
/iphone       iOS app
/windows/WinLock.Protocol  shared protocol models + error codes
/windows/WinLock.Cryptography  shared Ed25519 + DPAPI storage + identity
/windows/WinLock.Service   background service
/windows/WinLock.Tray      tray/status app
/tests        .NET tests
/docs         architecture, security, protocol, setup, threat model, windows-unlock
/scripts      setup/build/run/test scripts
```

## Phases

1. ✅ Repository + architecture
2. ✅ Windows service + authenticated API + lock
3. ✅ Secure pairing
4. ✅ Challenge-response authentication
5. ✅ iPhone application (Expo / React Native — TS verified, iOS build on macOS)
6. ✅ BLE proximity
7. ✅ Automatic lock
8. ✅ Windows unlock research / extension point