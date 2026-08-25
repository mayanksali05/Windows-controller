# Security Model

## 1. Principles

- Public-key cryptography only. No shared secrets, no hardcoded passwords, no
  hardcoded keys, no plaintext private keys.
- Nothing on the network is trusted by position (IP/MAC/Wi-Fi presence).
  Trust is established by cryptographic proof of key possession.
- BLE presence is a proximity signal only, never an authentication factor.
  The per-device service UUID (RFC 4122 v5 of the device id) lets the Windows
  scanner identify *which* phone is present, but the signal grants nothing:
  privileged operations always require Face ID + the signed challenge-response
  over Wi-Fi.
- The system never handles, transmits, or stores Windows credentials.
- No mechanism bypasses, disables, or circumvents Windows authentication.

## 2. Keys and Storage

### Windows (DPAPI)

- Windows device identity key pair (Curve25519/Ed25519) generated on first run.
- Stored in a DPAPI-protected file (`CurrentUser` scope). DPAPI ties decryption
  to the service account on the local machine.
- Authorized iPhone public keys stored in a DPAPI-protected key-value store.
- Device identifiers and configuration secrets likewise DPAPI-protected.

### iPhone (Keychain)

- iPhone identity key pair generated during pairing, stored in Keychain with
  `kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly`.
- Windows public key + TLS certificate public key (pin) stored in Keychain.

## 3. Transport Security (TLS)

- HTTPS only. Kestrel serves on a configurable port bound to the LAN interface.
- **Development:** a locally generated development certificate is used. The
  iPhone app only trusts a server whose certificate matches the **TLS pin**
  delivered in the pairing QR (SHA-256 of the leaf certificate DER). Pinning
  happens *before* the first connection, so pairing never relies on trusting
  arbitrary certificates. `acceptAllCertificates = true` and equivalents are
  forbidden everywhere.
- **Production:** a proper certificate issued for the laptop identity,
  validated and pinned (the app additionally requires the OS chain to validate
  in production mode).
- Development vs production is an explicit configuration value
  (`Server:Environment`), never inferred silently.

## 4. Authentication Protocol

See `protocol.md` for messages. Security-relevant properties:

- One-time challenges (cryptographically secure random nonces, min 32 bytes),
  bound to the requesting device and consumed on first use.
- Challenge lifetime bound (`Security:ChallengeLifetimeSeconds`, default 30).
- Timestamp skew check (`Security:MaxClockSkewSeconds`).
- Single-use replay cache: a consumed challenge is rejected.
- Signatures over `device_id ‖ challenge ‖ timestamp ‖ target_endpoint`
  (Ed25519, deterministic).
- Face ID (`LAContext`) gates the signing operation on the iPhone.
- Sessions: after `/auth/verify` the service returns a short-lived HMAC-SHA256
  signed token (`Security:SessionLifetimeMinutes`) whose signing key lives in
  memory for the process lifetime (sessions end on restart). Every privileged
  request re-checks device authorization, so unpairing revokes immediately.
- The tray application authenticates as the laptop itself using the Windows
  identity key through the same protocol — no dev token exists.

## 5. Privileged Operations

`/lock`, `/status`, `/proximity`, `/unpair` require a valid session token
produced by `/auth/verify`. Requests without a valid token are rejected with a
structured error; no privileged endpoint has an unauthenticated path.

## 6. API Hardening

- Bind only to configured interfaces; no public-Internet exposure.
- Request size limits (e.g. 16 KB) and strict JSON parsing.
- Rate limiting per source IP and per device ID.
- Inactive request timeouts.
- Structured, generic error messages that do not leak implementation details.
- Windows Firewall rule scoped to the local subnet during setup.

## 7. Logging

Security events (see `architecture.md`) are logged with timestamps and
diagnostic metadata. Never logged: private keys, passwords, tokens, or raw
cryptographic secrets. See `Logging` in the Windows service.

## 8. Explicit Non-Goals / Prohibitions

- Simulated keyboard password entry.
- Credential dumping or extraction.
- Disabling Windows login/security features.
- Registry/policy modification to bypass authentication.
- Trusting devices by MAC, IP, or Wi-Fi membership alone.

## 9. Windows Unlock

Unlocking a locked session requires OS-supported integration. Research
(`docs/windows-unlock.md`) found that:

- A **Credential Provider** is the only supported logon integration point, but
  it is not an enforcement mechanism — the user's real Windows Hello gesture,
  PIN, or password is still required on the laptop.
- **Windows Hello / FIDO2 / passkeys** only apply through OS-managed enrollment
  (e.g., the iPhone enrolled as a Windows Hello security key).

Neither is implementable as a safe companion-service feature today. The codebase
exposes `IWindowsAuthenticationProvider` as a documented extension point with no
implementation and no `/unlock` endpoint. No keyboard-injection or
Winlogon-bypass approach is shipped.

## 10. Residual Risks (summary)

See `threat-model.md` for the full matrix. Notable residual risks:

- A paired iPhone physically stolen: Face ID + device passcode mitigate; the
  user can unpair remotely/on the laptop.
- A compromised laptop: DPAPI secrets are readable by the compromised service
  account; nothing protects a fully compromised machine.
- BLE replay/spoofing: BLE is proximity-only, so impact is limited to
  auto-lock behavior, never access.