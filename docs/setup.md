# Setup

## Prerequisites

- **Windows 10/11** (64-bit, build 19041+) for the companion service (BLE
  proximity uses the WinRT Bluetooth LE advertisement API).
- Bluetooth 4.0+ hardware on the Windows machine, with Bluetooth enabled, for
  proximity detection. If Bluetooth is unavailable, proximity stays `UNKNOWN`
  and the service still functions (no BLE = no proximity/auto-lock).
- **.NET SDK 8.0** or later.
- **Xcode 15+ / iOS 17+ SDK** on macOS for the iPhone app (cannot build on
  Windows).
- A local network with Wi-Fi and Bluetooth.

## 1. Windows Service

```powershell
# from repo root
.\scripts\setup-windows.ps1     # creates config, firewall rule, dev cert
.\scripts\build-windows.ps1     # restores + builds
.\scripts\run-development.ps1   # runs the service in console mode (dev)
.\scripts\run-tests.ps1         # runs all .NET tests
```

First run generates the device identity key pair (DPAPI) and a development
certificate. The service prints the pairing QR/code on the console/tray.

For production, the service can be installed as a Windows Service
(`sc.exe create` or a provided install script) with recovery options so it
starts automatically and restarts on crash.

## 2. iPhone App

Open `iphone/` and generate the project with XcodeGen (`brew install xcodegen`):

```bash
cd iphone
xcodegen generate
open WinLock.xcodeproj
```

(Alternatively create the project manually in Xcode and add the `WinLock/`
sources.) Required capabilities (Info.plist / entitlements):

- **Bluetooth Always** (`NSBluetoothAlwaysUsageDescription`) — BLE proximity
  (used in a later phase; declared now so no crash occurs when BLE starts).
- **Local Network** (`NSLocalNetworkUsageDescription`) — discovery and HTTPS to
  the laptop.
- **Face ID** (`NSFaceIDUsageDescription`) — LocalAuthentication gate.
- **Camera** (`NSCameraUsageDescription`) — QR pairing.
- **Keychain** — via the `keychain-access-groups` entitlement.
- **Background modes:** `bluetooth-central` / `bluetooth-peripheral` if
  proximity should keep working in the background.

Do not request unused permissions (the camera sheet is only shown during
pairing; Bluetooth is only used once the proximity feature starts).

## 3. Pairing Procedure

1. Start the Windows service; open the tray → Pair device. A QR/barcode with
   the Windows device ID, public key, pairing nonce, and one-time token is
   shown.
2. In the iPhone app: Laptop → Add Laptop → Scan QR.
3. The iPhone generates its key pair, stores it in Keychain, and confirms
   pairing (signed proof of key possession).
4. Windows stores the iPhone public key (DPAPI) and marks the device authorized.
5. Verify on both sides that pairing completed. The iPhone now pins the
   Windows certificate public key.

## 4. Configuration

`windows/WinLock.Service/appsettings.json` (values are not secrets):

```json
{
  "Server": { "Port": 8765, "Environment": "Development" },
  "Security": { "ChallengeLifetimeSeconds": 30 },
  "Proximity": { "Enabled": true, "AwayTimeoutSeconds": 30 }
}
```

Private keys and pairing secrets are never stored in configuration.

## 5. Development vs Production

- `Server:Environment=Development` uses a locally generated, explicitly
  trusted dev certificate (still pinned, never `acceptAll`).
- Production uses a proper certificate; see `security.md`.

## 6. Troubleshooting

- Service won't start: check `Security` event log and service logs; verify the
  DPAPI user scope matches the running account.
- iPhone cannot discover laptop: confirm Bonjour (`_mywinlock._tcp`) is
  advertised, firewall allows UDP 5353 on the LAN, and both devices share a
  subnet.
- Lock does not happen: confirm the service account has permission to call
  `LockWorkStation` (interactive session required).
- Pairing fails: re-generate the one-time token; tokens are single-use.

## 7. Windows Unlock

Not yet implemented (see `architecture.md` §8). Unlock remains an extension
point; no bypass is shipped.