# Migration Plan — SwiftUI → Expo / React Native (TypeScript)

**Status: implemented. The Expo client lives at `iphone/` (TypeScript + two
local Swift native modules); the legacy SwiftUI app is preserved under
`iphone/WinLock/`. The Windows service is the source of truth and was **not**
modified.

## 1. Source-of-truth inventory (from the Windows implementation + docs)

### 1.1 Transport & JSON

- Base URL: `https://<laptop>:<port>/api/v1` (default port 8765). LAN only.
- JSON keys are **camelCase** (ASP.NET Web API defaults — verified: no custom
  `JsonSerializerOptions` is configured; the pairing QR payload also uses
  `JsonSerializerDefaults.Web` = camelCase). `docs/protocol.md` shows snake_case
  examples; the actual wire is camelCase.
- Envelope:
  - success: `{ "success": true, "message"?: string, "data"?: T }`
  - failure: `{ "success": false, "error": { "code": string, "message": string } }`
- Error codes: `AUTH_FAILED`, `CHALLENGE_EXPIRED`, `CHALLENGE_REPLAYED`,
  `DEVICE_UNKNOWN`, `DEVICE_UNAUTHORIZED`, `PAIRING_INVALID`,
  `PAIRING_EXPIRED`, `LOCK_FAILED`, `RATE_LIMITED`, `MALFORMED_REQUEST`,
  `INTERNAL_ERROR`.

### 1.2 Endpoints (request → response `data`)

| Method/Route | Auth | Request body | Response `data` |
|---|---|---|---|
| `GET /api/v1/status` | Bearer | — | `{ isLocked: bool?, batteryPercent: int?, serviceVersion, environment, lockAvailable, proximity, security }` |
| `GET /api/v1/settings` | Bearer | — | `{ proximityEnabled, proximityAwayTimeoutSeconds, proximityNearbyRssiThreshold, automaticLockEnabled, autoLockAwayDurationSeconds }` |
| `POST /api/v1/lock` | Bearer | `{ deviceId }` | — (message `"Laptop locked successfully"`) |
| `POST /api/v1/pair/request` | anon | `{ deviceId }` | `{ deviceId, windowsPublicKey, pairingAvailable, pairingNonce?, expiresAt?, signature? }` |
| `POST /api/v1/pair/session` | Bearer | — | `{ version, deviceId, windowsPublicKey, pairingNonce, pairingToken, expiresAt, signature, tlsPin }` (QR payload) |
| `POST /api/v1/pair/confirm` | anon | `{ deviceId, clientDeviceId, clientPublicKey, pairingToken, signature }` | — |
| `GET /api/v1/pair/devices` | Bearer | — | `[ { deviceId, name, pairedAt } ]` |
| `POST /api/v1/unpair` | Bearer | `{ deviceId }` | — |
| `POST /api/v1/auth/challenge` | anon | `{ deviceId }` | `{ challengeId, challenge, expiresAt }` |
| `POST /api/v1/auth/verify` | anon | `{ clientDeviceId, challengeId, timestamp, signature }` | `{ sessionToken, sessionExpires, proximity }` |
| `GET /api/v1/proximity` | Bearer | — | `{ state, deviceId?, rssi?, updatedAt }` |
| `POST /api/v1/unlock` | — | — | **does not exist (must 404)** |

### 1.3 Cryptography (must be byte-for-byte identical)

- **Ed25519** (RFC 8032): raw 32-byte private seed, 32-byte public key,
  64-byte deterministic signature.
- **Base64url** (RFC 4648 §5): `-`/`_`, no padding — used for keys, nonces,
  signatures, TLS pin.
- **Canonical signing strings** (UTF-8, fields joined `0x1F`, single trailing
  `0x1E`):
  - pairing: `clientDeviceId \x1f pairingNonce \x1e`
  - auth: `clientDeviceId \x1f challenge \x1f timestamp \x1f "/api/v1/auth/verify" \x1e`
  - The `timestamp` signed is the **verbatim** ISO8601 string sent.
- **iPhone device id**: `HEX(SHA256(publicKeyBytes))[0..8]` (16 hex chars).
- **TLS pin**: `base64url(SHA256(leafCertificateDER))`, compared as an exact
  string. Pinning happens **before** the first connection (pin from QR).
- **BLE service UUID**: RFC 4122 v5 over
  `namespace = 9B2F6D21-8E4C-4E2A-9F6A-9D4E3B2C1A00` + `deviceId`, bytes in
  network order, version-5/variant bits. Known-answer (from .NET):
  `ProximityUuid.ForDevice("PHONE12345678ABCD") = 3a1126f5-eb3b-51c8-9164-9b40a19c7341`.
- **Session token**: opaque HMAC-signed string; client treats it as opaque and
  checks `sessionExpires`.

### 1.4 Known-answer vectors (generated from the real .NET code — used in TS tests)

Ed25519 seed = bytes `01..20`:
- pubkey = `79B5562E8FE654F94078B112E8A98BA7901F853AE695BED7E0E3910BAD049664`
- deviceId = `65B60673D6ED884B`
- sign(`DEVICE\x1fNONCE\x1e`) = `6C82F6CCB54A869C1342505EE91A1B4220FFD19CABFEEFBBAD626326133F6209D2B3E921BAD48150070BD24AB9B4C4338D437D94F0773E4F07EEEECBDF4A5D06`

### 1.5 Discovery (Bonjour)

- Service `_mywinlock._tcp`, TXT `device_id`, `version=1`. Instance name
  `WinLock-<devId[:6]>`, hostname `winlock-<devId[:6].lower()>.local.`.
- The phone browses and resolves host+port; discovery grants nothing until
  pairing.

### 1.6 BLE

- Phone **advertises**: service UUID `v5(deviceId)`, local name = `deviceId`,
  read characteristic `C0FFEE00-1111-4222-8333-444455556666` returning
  `deviceId` UTF-8.
- **Windows scans** (WinRT). The phone learns its proximity by reading
  `/status.proximity` / `/proximity`. States: `UNKNOWN`, `NEARBY`, `AWAY`;
  `AUTHENTICATED` is derived client-side (nearby + valid session). BLE is
  proximity-only, never authentication.

### 1.7 Authentication state machine

`none` → `POST /auth/challenge` → **Face ID** → sign canonical → `POST
/auth/verify` → `sessionToken`/`sessionExpires` → `authenticated` → privileged
calls with `Authorization: Bearer`. On HTTP 401 → clear token, re-authenticate
once. `unpair` revokes immediately server-side.

## 2. Swift → Expo mapping

| Swift (iphone/WinLock) | Expo/TS equivalent |
|---|---|
| `CryptoKit Curve25519.Signing` (`DeviceKeys`) | `@noble/ed25519` (RFC 8032, raw keys) + `expo-crypto` random bytes |
| `ProtocolStrings` | `src/crypto/protocolStrings.ts` |
| `Base64URL` | `src/crypto/base64url.ts` |
| `ProximityUuid` (CryptoKit SHA1) | `@noble/hashes` `sha1` + `src/crypto/proximityUuid.ts` |
| `SHA256` deviceId | `@noble/hashes` `sha256` |
| `ServerTrustPinner` (URLSession delegate) | native module `winlock-networking` (NSURLSession delegate, same logic) |
| `APIClient` | `src/api/windowsApiClient.ts` (typed, retry/401/timeouts) |
| `FaceIDAuthenticator` | `expo-local-authentication` |
| `KeychainStore` | `expo-secure-store` |
| `PairedLaptopStore` (UserDefaults metadata) | `@react-native-async-storage/async-storage` (metadata only) |
| `BonjourDiscovery` (NWBrowser) | native module `winlock-networking` (NWBrowser) |
| `ProximityAdvertiser` (CoreBluetooth) | native module `winlock-bluetooth` (CoreBluetooth) |
| `PairingViewModel` | `src/pairing/pairingService.ts` |
| `ServiceContainer` | `src/services/` singletons |
| SwiftUI views | `app/` expo-router screens + `src/hooks/` |

## 3. Native modules (Expo local modules, Swift, autolinked)

1. **`modules/winlock-networking`** — required because React Native's `fetch`
   cannot do custom TLS trust evaluation, and Expo has no Bonjour API.
   - `pinnedRequest({ url, method, headers, body, pin }) → Promise<{ status, body }>`
     via `NSURLSession` + a `ServerTrustPinner` delegate (leaf DER → SHA-256 →
     base64url vs pin; mismatch → distinct certificate error). Never
     `acceptAll`.
   - `startDiscovery()/stopDiscovery()` via `NWBrowser("_mywinlock._tcp")`,
     TXT `device_id`, resolve host+port (same NWConnection technique as Swift).
     Event `onLaptopDiscovered { name, deviceId, host, port }`.
2. **`modules/winlock-bluetooth`** — CoreBluetooth `CBPeripheralManager`:
   `startAdvertising(deviceId)/stopAdvertising()`, event `onStateChange`.
   Advertises `v5(deviceId)` service + identity characteristic + local name.

The TS `BluetoothService` interface stays platform-independent (advertise,
proximity from server, `UNKNOWN|NEARBY|AWAY|AUTHENTICATED`).

## 4. Expo packages

- `expo`, `react-native`, `react` (SDK 54 / RN 0.81)
- `expo-router` (file-based navigation)
- `expo-dev-client` (development builds; required for native modules)
- `expo-local-authentication` (Face ID / passcode)
- `expo-secure-store` (Keychain-backed secure storage)
- `expo-crypto` (CSPRNG for key generation)
- `expo-camera` (QR pairing)
- `@react-native-async-storage/async-storage` (non-secret metadata only)
- `@noble/ed25519`, `@noble/hashes` (Ed25519, SHA-1/SHA-256)
- `expo-modules-core` (local modules)
- Dev: `typescript`, `jest-expo`, `@testing-library/react-native`

## 5. Project structure (spec-compliant)

```
iphone/                      # coexists with the preserved Swift iphone/WinLock
  app/                       # expo-router screens (index, laptop/[id], pair, settings, logs)
  src/
    api/        windowsApiClient.ts, errors.ts
    auth/       authenticationService.ts, faceIdGate.ts, session.ts
    bluetooth/  bluetoothService.ts (interface), proximityState.ts
    crypto/     ed25519.ts, base64url.ts, sha.ts, protocolStrings.ts, proximityUuid.ts, identity.ts
    discovery/  bonjourService.ts
    pairing/    pairingService.ts
    services/   lockService.ts, statusService.ts, settingsService.ts, logService.ts
    storage/    secureStore.ts, laptopStore.ts
    hooks/      useLaptops.ts, useProximity.ts, useStatus.ts
    types/      protocol.ts, errors.ts
    utils/      time.ts, json.ts
  modules/
    winlock-networking/    (expo module, Swift)
    winlock-bluetooth/     (expo module, Swift)
  assets/
  app.json  eas.json  package.json  tsconfig.json
```

## 6. Security parity (unchanged)

- Ed25519 challenge-response; no shared secrets; no hardcoded keys.
- Private key, Windows public keys, and TLS pins only in `expo-secure-store`
  (Keychain). Never AsyncStorage, never logged.
- TLS pinned before first connection (pin from QR); production mode also
  requires the OS chain.
- Face ID before signing the challenge, and an explicit Face ID gate before
  **lock**, **pairing confirmation**, and **unpair**.
- BLE proximity never authenticates. No Windows-auth bypass; no `/unlock`.
- Auto-lock remains Windows-side (`AutomaticLockMonitor`); the app only reads
  `/settings`.

## 7. Deliberate differences from Swift

1. CryptoKit → `@noble/ed25519` (same RFC 8032 raw formats; cross-checked by
   known-answer vectors from .NET).
2. URLSession in-process pinning → native-module pinning (same semantics).
3. Explicit Face ID gate on lock/unpair/pair (stricter than Swift, per new
   requirements).
4. Proximity is read back from the Windows server (the phone never scans).

## 8. Testing

- Unit (jest-expo): base64url, protocolStrings bytes, Ed25519 known-answers,
  deviceId derivation, UUIDv5 (`3a1126f5-...`), envelope/error parsing,
  authentication state machine, pairing state machine, device-trust logic.
- Native modules mocked in unit tests; native behavior verified only on a
  device (not claimable here).

## 9. Build & run

- `npm install`; `npx expo start` (development build); `eas build --platform ios
  --profile development`. Native modules require a development build — Expo Go
  is **not** supported.

## 10. Risks / blockers

- Cannot build/test on this Windows machine (iOS + Swift modules require macOS).
  TS is type-checked (`tsc --noEmit`) and pure-TS unit tests run via jest, but
  the app is **not** claimed to work on hardware until tested there.
- expo-secure-store value-size limits: fine for 32-byte keys/pins.
- Hermes needs `TextEncoder` (present in modern RN); crypto is pure TS.

## 11. Implementation status

Implemented in `iphone/`:
- `app/` expo-router screens (laptop list, detail/lock, pair, settings, logs).
- `src/` — typed `WindowsApiClient`, crypto (`@noble/ed25519`, base64url,
  canonical strings, UUIDv5), Face ID gate, pairing, discovery, BLE, storage
  (secure-store + AsyncStorage), hooks, services.
- `modules/winlock-networking` and `modules/winlock-bluetooth` (Swift, iOS-only,
  autolinked — verified with `expo-modules-autolinking`).
- Unit tests: 36 passing (crypto known-answers cross-checked against the .NET
  implementation, protocol serialization, auth state machine, pairing state,
  device trust, error handling).

Blocked on this machine (requires macOS/Xcode + a physical iPhone):
- Building/running the iOS app, exercising the native modules, BLE advertising,
  Bonjour discovery, Face ID, and on-device pairing/lock.

Verified here: `npm install`, `tsc --noEmit`, `jest` (36/36), `expo config`
(plugins resolve), `expo-modules-autolinking` (both native modules discovered).