# Installing the iPhone app from Windows (no Mac, free Apple ID)

GitHub builds the app on one of its Mac machines (an **unsigned** `.ipa`).
**Sideloadly** on Windows signs it with your **free Apple ID** and installs it
over USB.

Free-account limits (Apple's rules): the app must be **re-signed every 7 days**
(Sideloadly can refresh it automatically), and you can have at most 3
sideloaded apps at a time. A paid Apple Developer account ($99/yr) removes both
limits.

## 1. Build the .ipa on GitHub

1. Commit and push this repo to GitHub (`mayanksali05/Windows-controller`). The
   workflow is in `.github/workflows/ios-unsigned-ipa.yml`.
2. On GitHub: **Actions → iOS unsigned IPA → Run workflow**. It also runs
   automatically on every push that changes `iphone/`.
3. Wait about 15–25 minutes. Open the finished run, then under **Artifacts**
   download **WinLock-unsigned-ipa** and unzip it. You get
   `WinLock-unsigned.ipa`.

> Private repos: GitHub's free plan includes 2,000 Actions minutes a month,
> and macOS minutes count 10×, so expect roughly 8–10 builds a month.
> Public repos build for free.

## 2. One-time Windows setup

1. Install **iTunes** and **iCloud** from Apple's website (the
   apple.com/itunes download), **not** the Microsoft Store versions. Sideloadly
   needs Apple's drivers from them.
2. Install **Sideloadly** from https://sideloadly.io.
3. Connect the iPhone by USB, unlock it, and tap **Trust This Computer**.

## 3. Install

1. Open Sideloadly, drag `WinLock-unsigned.ipa` into it, and enter your Apple
   ID. Using a secondary Apple ID is fine.
2. Tick **automatic refresh** (keeps the app alive past 7 days while the
   iPhone is reachable over USB or Wi-Fi). Then click **Start**.
3. On the iPhone:
   - **Settings → Privacy & Security → Developer Mode → On** (iOS 16+; the
     phone restarts).
   - **Settings → General → VPN & Device Management →** your Apple ID →
     **Trust**.
4. Open WinLock. Allow **Local Network** (needed to find the laptop),
   **Camera** (QR scan), **Face ID**, and **Bluetooth** when asked.

## 4. Every 7 days

If automatic refresh didn't run, the app stops opening. Drag the same `.ipa`
into Sideloadly again with the **same Apple ID** and click Start. The app's
data (pairing, saved unlock password) is kept. Don't delete the app first.

## 5. Use it

Laptop: run the service and tray (`docs/setup.md`), install the credential
provider for unlock (`docs/windows-unlock.md`). Then in the app: scan the
pairing QR → open the laptop → **Unlock password → Set password** → **Lock** /
**Unlock**.

## Troubleshooting

| Problem | Fix |
|---|---|
| Sideloadly: bundle ID / App ID error | The bundle ID `com.mayanksali05.winlock` (in `iphone/app.json`) must be unique to you. Change it in `app.json` and rebuild, or set a custom bundle ID in Sideloadly's advanced options. Keep using the same one afterwards. |
| "Maximum number of apps" | The free account allows 3 sideloaded apps. Remove one, or wait for Apple's App ID limit to reset. |
| App doesn't find the laptop | Check that Local Network is allowed (Settings → WinLock), both devices are on the same Wi-Fi, and the laptop firewall allows TCP 8765 and UDP 5353 (mDNS). |
| Device not shown in Sideloadly | Reinstall the apple.com iTunes, replug, re-trust the computer. |
| Workflow fails at `xcodebuild` | Open the run's log; the last 200 lines of the build are printed. |
