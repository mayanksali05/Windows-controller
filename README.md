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
- **`iphone/` — SwiftUI app (iOS 17+)** — discovery (Bonjour), QR pairing,
  Face ID + CryptoKit signatures, status, lock button, proximity, settings, logs.

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

See `docs/setup.md` for required capabilities (Bluetooth, Local Network, Face ID,
Keychain). Open `iphone/` in Xcode and run on a real device (BLE + Face ID do
not work on the simulator).

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

## 10. BLE Requirements

- iPhone advertises a custom BLE service; Windows scans for paired devices.
- Proximity states: `UNKNOWN`, `NEARBY`, `AWAY`, `AUTHENTICATED`.
- Proximity is **not** authentication.

## 11. Testing

```powershell
.\scripts\run-tests.ps1
```

Security tests: invalid signatures, expired/replayed challenges, unknown
devices, malformed requests, unauthorized lock, pairing failures.
API tests: auth success/failure, lock, status, pair, unpair.
Windows tests: lock invocation, service lifecycle, config, secure storage.

## 12. Troubleshooting

See `docs/setup.md` §6.

## 13. Known Limitations

- No public-Internet deployment; LAN-only by design.
- iOS build requires a Mac.
- Fully compromised hosts can read DPAPI-protected data (OS boundary).

## 14. Windows Unlock Limitations

Unlocking a locked session from an external device requires an OS-supported
integration (Credential Provider, Windows Hello/FIDO2). This is represented by
`IWindowsAuthenticationProvider` and is a **documented extension point**; it is
not faked, and no Windows-auth bypass is shipped. See `docs/architecture.md` §8.

## Repository Layout

```
/iphone       iOS app
/windows/WinLock.Protocol  shared protocol models + error codes
/windows/WinLock.Cryptography  shared Ed25519 + DPAPI storage + identity
/windows/WinLock.Service   background service
/windows/WinLock.Tray      tray/status app
/tests        .NET tests
/docs         architecture, security, protocol, setup, threat model
/scripts      setup/build/run/test scripts
```

## Phases

1. ✅ Repository + architecture
2. ✅ Windows service + authenticated API + lock
3. ✅ Secure pairing
4. ✅ Challenge-response authentication
5. ⬜ iPhone application
6. ⬜ BLE proximity
7. ⬜ Automatic lock
8. ⬜ Windows unlock research / extension point