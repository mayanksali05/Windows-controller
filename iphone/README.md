# WinLock iOS App

SwiftUI app (iOS 17+) for securely controlling a Windows laptop running the
`WinLock.Service`.

## Requirements

- macOS with **Xcode 15+** and the iOS 17 SDK.
- **XcodeGen** (recommended) to generate the Xcode project from `project.yml`:
  `brew install xcodegen`. Alternatively create the project manually (see
  `docs/setup.md` → iPhone setup).
- A real iPhone for pairing, Bluetooth, and Face ID (the simulator cannot
  provide BLE or Face ID).

## Generate and build

```bash
cd iphone
xcodegen generate          # creates WinLock.xcodeproj
open WinLock.xcodeproj
```

In Xcode:

1. Select the **WinLock** scheme and a device target.
2. Set your **Team** under Signing & Capabilities (a personal Apple ID is
   enough; the app uses Face ID and a Keychain access group).
3. Run on a real device.

## What the app does

- Discovers WinLock laptops over **Bonjour** (`_mywinlock._tcp`).
- **Pairs** by scanning the QR code shown on the Windows laptop (tray → Pair
  new device). The QR carries the Windows device id, public key, one-time
  pairing token/nonce, a signature, and the **TLS pin**.
- Stores the iPhone identity private key in the **Keychain** (Ed25519 via
  CryptoKit) and pins the laptop's TLS certificate to the exact certificate
  seen at pairing.
- Authenticates with **Face ID + challenge-response** and a short-lived session
  token; every privileged action (lock, status, unpair) requires it.
- Shows laptop status (locked state, battery, proximity, security) and a
  **LOCK** button.

## Required capabilities

Declared in `Info.plist` / `WinLock.entitlements`:

| Capability | Why |
|------------|-----|
| Local Network (`NSLocalNetworkUsageDescription`) | Bonjour discovery and HTTPS to the laptop |
| Camera (`NSCameraUsageDescription`) | QR pairing |
| Face ID (`NSFaceIDUsageDescription`) | Gate privileged actions via `LocalAuthentication` |
| Bluetooth Always (`NSBluetoothAlwaysUsageDescription`) | BLE proximity (used in a later phase; declared now) |
| Keychain access group | Store keys/pins in the Keychain |
| Background modes: bluetooth-central/peripheral | Keep BLE proximity working in the background (later phase) |

No other permissions are requested.

## Security notes

- Private keys live only in the Keychain
  (`kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`).
- TLS is pinned; the app never accepts arbitrary certificates
  (`ServerTrustPinner` compares the leaf certificate DER hash to the pairing
  pin).
- BLE presence is a proximity signal only and never authorizes anything.

## Layout

```
project.yml                      XcodeGen project definition
WinLock/
  App/          app entry + service container
  Models/       protocol DTOs
  Networking/   APIClient (TLS pinned), Bonjour discovery
  Security/     CryptoKit keys, Keychain, Base64URL, canonical strings, Face ID
  Storage/      paired-laptop metadata + local security log
  ViewModels/   list, detail, pairing
  Views/        SwiftUI screens
```