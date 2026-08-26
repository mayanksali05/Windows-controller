# WinLock iOS Client

An **Expo (SDK 54) / React Native (TypeScript)** client for securely controlling
a Windows laptop running `WinLock.Service`. It is the current client; the legacy
SwiftUI app is preserved in `WinLock/` for comparison.

## Requirements

- macOS with **Xcode 15+** for building the iOS app (development builds —
  **Expo Go is not supported** because the app ships two local native modules).
- A physical iPhone for pairing, Face ID, and Bluetooth.
- An Expo account for `eas build`.

## Install & typecheck

```bash
cd iphone
npm install
npx tsc --noEmit        # typecheck
npx jest                # unit tests (protocol, crypto, auth, pairing)
```

## Build & run on a physical iPhone

```bash
eas build --platform ios --profile development   # development build
# install the built .ipa on the iPhone, then:
npx expo start --dev-client                       # scan the QR, run in the dev client
```

Local iOS module changes require a new development build (`eas build`).

## Project layout

```
app/                      expo-router screens (list, laptop/[id], pair, settings, logs)
src/
  api/        typed WindowsApiClient (pinned HTTPS, retries, 401 re-auth)
  auth/       Face ID gate, session, authentication service
  bluetooth/  platform-independent BLE service + proximity state
  crypto/     base64url, sha, ed25519 (@noble), protocolStrings, proximityUuid, identity
  discovery/  Bonjour service (native module events)
  pairing/    pairing service (payload verify + signed confirm + persist)
  services/   app container, lock/status/settings/log/proximity services
  storage/    expo-secure-store (keys/pins) + AsyncStorage (metadata)
  hooks/      useLaptops, useLaptopDetail, useProximity, useLogs
  native/     TS bindings for the native modules
  types/      protocol DTOs + error codes
  utils/      hex, time helpers
modules/
  winlock-networking/   Swift expo module: pinned HTTPS (NSURLSession) + Bonjour
  winlock-bluetooth/    Swift expo module: CoreBluetooth proximity advertising
assets/                 app icons
app.json  eas.json  package.json  tsconfig.json  jest.config.js
```

## Security

- Ed25519 (RFC 8032) keys/signatures interoperable with the Windows service;
  the iPhone identity seed, Windows public keys, and TLS pins live only in
  `expo-secure-store` (iOS Keychain). Never AsyncStorage.
- TLS is pinned to the exact certificate from the pairing QR (native module),
  before the first connection; arbitrary certificates are never accepted.
- Face ID (`expo-local-authentication`) gates lock, pairing confirmation, and
  unpairing, plus the challenge signing inside authentication.
- BLE is a proximity signal only and never authenticates.
- Windows unlock is an extension point and is **not** implemented.

## Required permissions (app.json)

Local Network, Camera (QR pairing), Face ID, Bluetooth Always (advertising), and
Keychain (secure-store). Background Bluetooth modes are declared for proximity
advertising.