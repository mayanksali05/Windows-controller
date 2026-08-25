# Threat Model

Scope: the iPhone ⇄ Windows lock system operating on a home/office LAN. Assets:

- Windows device identity private key (DPAPI).
- Authorized iPhone public-key list (DPAPI).
- Session tokens and challenges (in memory).
- iPhone identity private key (Keychain).
- The Windows session itself (locked vs unlocked state).
- Security event logs.

Each entry records attack, impact, mitigation, and residual risk.

---

## T1. Attacker connected to the same Wi-Fi

- **Attack:** discovers the service via port scan/mDNS, attempts to call
  `/lock`, `/status`, or participate in pairing.
- **Impact:** without a paired key and valid session, all privileged endpoints
  reject. Worst realistic impact: network noise / DoS of the service.
- **Mitigation:** TLS + pinned server cert, signed challenges, session tokens,
  rate limiting, request size limits, LAN-scoped firewall rule, no
  IP/MAC-based trust.
- **Residual risk:** Low. LAN DoS remains possible (out of scope); privileged
  control is not achievable.

## T2. Attacker discovers the Windows API port

- **Attack:** probes endpoints with malformed/unauthenticated requests.
- **Impact:** only generic errors returned; no key material or device data
  leaked beyond device-id presence.
- **Mitigation:** strict validation, size limits, rate limiting, generic error
  messages, no secrets in responses.
- **Residual risk:** Low. Minor information disclosure (device-id) acceptable.

## T3. Attacker replays an old authentication message

- **Attack:** captures a signed `/auth/verify` payload and replays it.
- **Impact:** would otherwise obtain a session.
- **Mitigation:** one-time challenges (replay cache rejects reused nonces),
  timestamp skew check, short challenge lifetime, session tokens bound to a
  single issue time and expiry.
- **Residual risk:** Low. Window is limited to the challenge lifetime and a
  single use.

## T4. Attacker steals the iPhone

- **Attack:** uses the phone to lock the laptop or attempt unlock.
- **Impact:** locking a machine requires only possession + the app; this is
  acceptable (locking is not harmful). Any privileged action still requires
  Face ID / passcode.
- **Mitigation:** Face ID gate on signing, Keychain accessible only after first
  unlock, user can unpair the device from the laptop.
- **Residual risk:** Medium for lock (intended feature); Low for anything else.
  Unlock remains unsupported.

## T5. Attacker obtains the laptop

- **Attack:** reads DPAPI files, attempts to extract the Windows private key or
  iPhone public keys, or modifies the service binary.
- **Impact:** on a fully compromised machine the attacker can read what the
  service account can read and can impersonate the laptop.
- **Mitigation:** DPAPI ties keys to the user/machine; service runs with least
  privilege; keys are never written to config; the OS session itself remains
  protected by Windows login (not bypassed by this system).
- **Residual risk:** Medium-high. A fully compromised host defeats DPAPI; no
  software on that host can protect it. This is an OS-boundary limitation, not
  solvable in-app.

## T6. Attacker spoofs BLE advertisements

- **Attack:** advertises the iPhone's BLE service to trigger auto-lock bypass or
  proximity state confusion.
- **Impact:** proximity is a signal, not an authentication factor. Worst case:
  auto-lock behavior is affected (e.g., laptop stays unlocked while the real
  phone is away, or locks while present).
- **Mitigation:** BLE never grants privileged access; automatic lock is a
  convenience control. BLE advertisement carries a session-random value that is
  not secret but is correlated with the authenticated channel.
- **Residual risk:** Medium for the proximity convenience feature; none for
  security of privileged commands.

## T7. Attacker captures an old authentication message

- **Attack:** stores a captured challenge/signature offline for later replay.
- **Impact:** same as T3 — replay protection applies.
- **Mitigation:** challenge nonces are single-use and time-bound; signatures are
  bound to endpoint + timestamp; session tokens expire.
- **Residual risk:** Low.

## T8. Attacker attempts to modify the Windows application

- **Attack:** replaces binaries, patches memory, or injects DLLs.
- **Impact:** full control of whatever the service can do (lock; no
  credentials).
- **Mitigation:** code signing for releases, least-privilege service account,
  no secrets in the binary, no credential handling.
- **Residual risk:** Medium. Host-level compromise is out of scope for an
  application; the OS/AV boundary is the mitigation.

## T9. Attacker attempts to access stored keys

- **Attack:** reads config files, DPAPI blobs, Keychain, logs.
- **Impact:** if the Windows private key leaks, an attacker could impersonate
  the laptop during future pairings or decrypt nothing of value (no payload
  encryption relies on it). If an iPhone public key is tampered, pairing of a
  rogue device is possible.
- **Mitigation:** DPAPI-protected storage, Keychain
  `ThisDeviceOnly`, keys never in config/logs, integrity checks on the
  authorized-device store, pairing confirmation UI on both devices.
- **Residual risk:** Low-Medium, DPAPI subject to local account compromise.

## T10. Attacker attempts to bypass Windows authentication

- **Attack:** exploits this system to unlock the Windows session without
  credentials.
- **Impact:** would be a critical auth bypass.
- **Mitigation:** the system has **no** unlock path and no credential handling.
  `IWindowsAuthenticationProvider` is an abstraction with no implementation and
  no `/unlock` endpoint. Research (`docs/windows-unlock.md`) shows the only
  supported unlock integrations (Credential Provider, Hello/FIDO2 enrollment)
  keep Winlogon in control and still require the user's real credentials.
- **Residual risk:** None introduced by this design. Explicitly documented
  non-goal; any future unlock integration must go through the supported OS path.

---

## Assumptions

- The Windows host is not already compromised (see T5/T8).
- The LAN is untrusted for *authorization* but TLS provides confidentiality and
  integrity against passive/active MITM on the wire.
- BLE provides approximate physical proximity, not identity.

## Accepted Residual Risks Summary

| # | Risk | Level | Owner |
|---|------|-------|-------|
| T4 | Stolen iPhone used to lock | Low (intended) | UX/settings |
| T5 | Fully compromised host defeats DPAPI | Medium-high | OS boundary |
| T6 | BLE spoof affects auto-lock only | Medium | Feature |
| T8 | Local tampering with service | Medium | OS/AV boundary |