# Architecture

> Phase 1 document. Defines the components, communication flows, authentication,
> pairing, BLE proximity, and security boundaries for the iPhone-based Windows
> lock system. This document is authoritative for how the system is **designed**
> to work; where a mechanism cannot yet be implemented with a supported Windows
> API, that is called out explicitly rather than faked.

## 1. Overview

A two-component system that lets a single iPhone (or a small set of authorized
iPhones) interact with a Windows laptop over the local network:

1. **Remote lock** of the Windows session.
2. **BLE proximity detection** of the paired iPhone.
3. **Cryptographic, Face-ID-backed authentication** of the iPhone.
4. **Optional automatic locking** when the iPhone leaves proximity.

Windows session **unlocking** is deliberately *not* implemented via any bypass.
It is represented by the `IWindowsAuthenticationProvider` abstraction and, until
a supported integration exists (Credential Provider / Windows Hello / FIDO2), it
remains a documented extension point. See `security.md`.

## 2. Components

### 2.1 Windows Companion Service (`windows/WinLock.Service`)

A background service (.NET 8, ASP.NET Core / Kestrel) that:

- Hosts the versioned, authenticated local-network HTTPS API.
- Generates and stores the Windows device identity key pair (Curve25519).
- Stores authorized iPhone public keys via DPAPI-protected storage.
- Runs the pairing flow (QR/code based public-key exchange).
- Runs the challenge-response authentication protocol.
- Issues the remote lock command via `LockWorkStation` (P/Invoke).
- Scans for the paired iPhone's BLE advertisement for proximity estimation.
- Triggers configurable automatic lock on prolonged absence.
- Writes structured security logs.
- Exposes a small **tray application** (`windows/WinLock.Tray`) for status,
  pairing, settings, and log viewing. The tray authenticates to the service as
  the laptop itself using the shared Windows identity key; it holds no iPhone
  secrets.

Shared cryptography (`windows/WinLock.Cryptography`): Ed25519, DPAPI secure
storage, device identity, and the authorized-device store are used by both the
service and the tray so the tray can sign challenges with the laptop identity.

**Locking context note:** `LockWorkStation` only affects the interactive
session. In development the service runs in the console (interactive session)
and can lock directly. If installed as a Windows Service (Session 0), the
service cannot lock the user's desktop directly; the interactive tray/companion
app is the locking host. The status payload reports `lockAvailable` accordingly.

### 2.2 iPhone App (`iphone/`)

An **Expo / React Native (TypeScript)** app (the legacy SwiftUI client remains
in `iphone/WinLock/` for comparison). It provides:

- Laptop discovery via Bonjour/mDNS (`_mywinlock._tcp`), implemented in a local
  native module (`modules/winlock-networking`) because Expo has no Bonjour API.
- TLS-pinned HTTPS via the same native module (React Native `fetch` cannot do
  custom trust evaluation); pinning happens before the first connection using
  the pin from the pairing QR.
- Secure pairing (QR scan via `expo-camera`, Ed25519 via `@noble/ed25519`,
  `expo-secure-store` for keys).
- Challenge-response authentication with Face ID via `expo-local-authentication`.
- BLE proximity advertising via a local native module
  (`modules/winlock-bluetooth`, CoreBluetooth); proximity is read back from the
  Windows service (the laptop scans).
- Status display, remote lock button, proximity indicator, settings, and a
  local security-event log.

## 3. Communication

- **Application channel:** HTTPS over the local network. TLS only. In
  development a locally generated certificate is used and pinned by its public
  key after pairing; the client never silently accepts arbitrary certificates.
- **Transport security:** see `security.md` for development vs production cert
  handling and pinning.
- **Service binding:** binds only to the configured LAN interface(s) and port;
  no Internet exposure. Windows Firewall rule scoped to the local subnet is
  created during setup.

### API surface (`/api/v1`)

| Method | Route            | Auth required | Purpose                              |
|--------|------------------|---------------|--------------------------------------|
| GET    | `/status`        | Yes           | Laptop status, battery, proximity     |
| POST   | `/pair/request`  | No*           | Verify server identity (public info) |
| POST   | `/pair/session`  | Yes           | Create pairing session, QR payload    |
| POST   | `/pair/confirm`  | No*           | Confirm pairing with one-time token + signed challenge |
| GET    | `/pair/devices`  | Yes           | List paired devices                   |
| POST   | `/unpair`        | Yes           | Remove a paired device                |
| POST   | `/auth/challenge`| No*           | Request an auth challenge             |
| POST   | `/auth/verify`   | Yes (fresh)   | Verify signed challenge, issue session|
| POST   | `/lock`          | Yes           | Lock workstation                     |
| GET    | `/proximity`     | Yes           | Current proximity state               |
| GET    | `/settings`      | Yes           | Read proximity/auto-lock policy       |

`*` Pairing/auth endpoints are unauthenticated by design (no key yet). The
one-time pairing token is **never** exposed to unauthenticated clients — it is
only shown on the Windows screen as a QR code.

## 4. Authentication

Public-key challenge-response, no shared secrets.

1. Client requests `/auth/challenge` → service returns a fresh random nonce with
   an expiry (`ChallengeLifetimeSeconds`, default 30).
2. Client runs Face ID (`LAContext`), then signs `device_id ‖ challenge ‖
   timestamp ‖ endpoint` with its private key (CryptoKit, Curve25519 / Ed25519).
3. Client posts `/auth/verify` with the signature.
4. Service verifies: device is paired, challenge is unexpired and not previously
   seen (single-use replay cache), timestamp is within skew, signature validates
   against the stored iPhone public key.
5. On success the service issues a short-lived HMAC-SHA256 signed session token
   (in-memory signing key) used for subsequent privileged calls (`/lock`,
   `/status`). Every privileged request re-checks that the device is still
   authorized, so unpairing revokes sessions immediately.

Replay protection: single-use challenges, timestamp/skew checks, per-session
token expiry, and a bounded nonce cache. See `protocol.md`.

The tray application authenticates as "the laptop itself" through the same
protocol using the shared Windows identity key (see `Cryptography`).

## 5. Pairing

1. Windows service generates a device identity key pair on first run (DPAPI-
   protected); the device ID is derived from the public key.
2. The tray application creates a pairing session and shows a QR code
   containing the Windows device ID, public key, one-time nonce, one-time
   pairing token, a signature over the nonce, and the **TLS pin** (SHA-256 of
   the HTTPS leaf certificate).
3. iPhone generates its own key pair, scans the QR, verifies the Windows
   signature, and stores the Windows public key and TLS pin in Keychain.
4. iPhone sends its device ID, public key, and a signature over the pairing
   nonce (proving possession of its private key) to `/pair/confirm`, with TLS
   already pinned from the QR.
5. Windows verifies the token (single-use), the signature, stores the iPhone
   public key (DPAPI), and marks the device authorized.
6. Both sides mark the pairing complete; future requests require the signed
   challenge flow above.

Trust is established by physical possession of the QR scan, not by network
position. The one-time pairing token never travels over the network. See
`protocol.md` for the message format.

## 5.1 Discovery

The service advertises `_mywinlock._tcp` via a dependency-free mDNS responder
(`Discovery/MdnsResponder`), so the iPhone discovers the laptop over the LAN
without a fixed IP. Discovery is a convenience, never a trust signal: pairing
establishes trust, and BLE (Phase 6) is proximity-only.

## 6. BLE Proximity

- The iPhone advertises a **per-device BLE service UUID** derived from its
  device id with RFC 4122 v5 (`ProximityUuid`, mirrored on both sides), plus a
  characteristic that returns the device id. The Windows service
  (`Bluetooth/WindowsBluetoothProximityScanner`) scans with the supported WinRT
  `BluetoothLEAdvertisementWatcher` and maps matching service UUIDs back to
  paired devices, estimating proximity from RSSI and presence.
- **BLE presence is a proximity signal only. It is never sufficient for
  privileged operations.** Privileged operations always require cryptographic
  authentication + Face ID.
- Proximity states reported by the server: `UNKNOWN`, `NEARBY`, `AWAY`. The
  combined `AUTHENTICATED` state is derived client-side (nearby + a valid
  authenticated session).
- A configurable away timeout (`Security:ProximityAwayTimeoutSeconds`) absorbs
  temporary signal loss so a single dropped scan never locks the machine.
- `ProximityMonitor` (hosted service) keeps the scanner in sync with the
  paired-device store, exposes state for `/status`, `/proximity`, and the
  `auth/verify` response, and emits `PROXIMITY_CHANGED` events. When Bluetooth
  is unavailable, state is `UNKNOWN` (fail-safe).

### Automatic lock (Phase 7)

`AutomaticLockMonitor` (hosted service) implements optional automatic locking:

- When proximity leaves `NEARBY` (away or unknown), a timer starts. If the phone
  returns within `Security:AutoLockAwayDurationSeconds` (default 60), the timer
  is cancelled. If still absent when it fires, the workstation is locked via
  `LockWorkStation`.
- One lock per absence episode (no repeated lock attempts), skipped when the
  workstation is already locked or the process is not in an interactive session.
- It only arms when at least one device is paired, and only when
  `Security:AutomaticLockEnabled` is true. Proximity is a convenience signal —
  automatic lock never bypasses or weakens Windows authentication.

## 7. Security Boundaries

```
+--------------------------+        HTTPS/TLS (LAN)         +--------------------------+
|      iPhone App          | <----------------------------> |  Windows Companion Service|
|  Keychain (keys)         |                                |  DPAPI (keys)             |
|  CryptoKit signatures    |                                |  Auth/session logic       |
|  Face ID (LocalAuth)     |                                |  Lock controller          |
+--------------------------+                                |  BLE scanner              |
        | BLE advertisement                                 +--------------------------+
        +-------------------------------------------------->           |
                                                            +--------------------------+
                                                            |  Windows OS (LockWorkStation)
                                                            +--------------------------+
```

Boundaries and invariants:

- **Trust boundary 1 (iPhone app → service):** only cryptographically
  authenticated, authorized devices may issue privileged commands. Everything
  else (discovery, pairing, challenges) is unauthenticated by design but
  constrained by one-time nonces and local one-time pairing tokens.
- **Trust boundary 2 (service → OS):** the service can only lock the session via
  the supported `LockWorkStation` API. There is no credential handling on the
  service side, ever.
- **Key storage:** Windows private keys in DPAPI-protected files; iPhone private
  keys in Keychain. Keys never transit the wire.
- **No secrets in config:** configuration holds port, timeouts, environment — not
  keys or passwords.
- **Defense against LAN attackers:** TLS + pinned server identity + signed
  challenges + replay protection + rate limiting + request size limits. Same-Wi-Fi
  presence alone grants nothing.

## 8. Windows Unlock (extension point)

Research concluded (see `docs/windows-unlock.md`): unlocking a locked Windows
session is only possible through the OS credential stack (Winlogon). The
supported options are:

- **Windows Credential Provider** (C++ COM, admin install): the only supported
  logon integration point, but credential providers are *not* enforcement
  mechanisms — the LSA validates credentials, so the user's real Windows Hello
  gesture, PIN, or password is still required on the laptop. An iPhone could at
  most trigger such a flow, never replace the credential.
- **FIDO2 / WebAuthn passkeys**: an iPhone could be a FIDO2 authenticator only
  if enrolled as a Windows Hello security key for the account — a deep,
  user-interactive OS enrollment outside the scope of a companion service.

Neither is implementable as a safe companion-service feature today. The codebase
exposes `IWindowsAuthenticationProvider` as a clean boundary with **no
implementation** and **no `/unlock` endpoint**; the feature remains a documented
extension point. No keyboard-injection, credential-fabrication, or
Winlogon-bypass approach is shipped.

## 9. Repository Layout

```
/iphone                  iOS client (Expo / React Native; legacy SwiftUI in iphone/WinLock)
/windows/WinLock.Service Windows background service
/windows/WinLock.Tray    Tray/status application
/tests/Windows.Tests     .NET unit tests (security-critical paths)
/tests/Protocol.Tests    Cross-cutting protocol tests
/docs                    architecture, security, protocol, setup, threat model, windows-unlock
/scripts                 build/setup/run/test scripts
```

## 10. Phased Delivery

| Phase | Scope |
|-------|-------|
| 1 | Repository + architecture (this document) |
| 2 | Windows service + authenticated local API + lock |
| 3 | Secure pairing |
| 4 | Challenge-response authentication |
| 5 | iPhone application (SwiftUI, discovery, pairing, Face ID, lock) |
| 6 | BLE proximity |
| 7 | Automatic lock |
| 8 | Windows unlock research / extension point |