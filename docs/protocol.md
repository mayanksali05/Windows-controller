# Protocol

Versioned protocol for iPhone ⇄ Windows Companion Service communication.

Base URL: `https://<laptop>:<port>/api/v1`

All requests/responses are JSON over TLS. All failures use the structured error
shape:

```json
{ "success": false, "error": { "code": "AUTH_FAILED", "message": "Authentication failed" } }
```

Success responses use:

```json
{ "success": true, "message": "Laptop locked successfully" }
```

## 1. Discovery

- Windows advertises `_mywinlock._tcp` via Bonjour/mDNS on the LAN.
- The iPhone resolves the service to an IP:port, then performs pairing (below)
  before trusting anything.
- Discovery alone grants nothing.

## 2. Pairing

`POST /pair/request` (no auth — no key exists yet)

```json
{ "device_id": "<windows-device-id>" }
```

Windows validates the request, and if the requesting side is permitted to
pair (a local one-time pairing token is active), responds:

```json
{
  "success": true,
  "device_id": "<windows-device-id>",
  "windows_public_key": "<base64url>",
  "pairing_nonce": "<base64url>",
  "pairing_token": "<one-time-token>",
  "pairing_expires": "<ISO8601>"
}
```

The iPhone shows this as a QR/barcode scanned from the Windows screen instead of
a network call when the device is not yet paired; both paths carry the same
payload. The iPhone generates its own key pair, then:

`POST /pair/confirm`

```json
{
  "device_id": "<windows-device-id>",
  "client_device_id": "<iphone-device-id>",
  "client_public_key": "<base64url>",
  "pairing_token": "<one-time-token>",
  "signature": "<base64url sig over pairing_nonce>"
}
```

Windows verifies the signature against the presented public key (proving key
possession), consumes the one-time token, and stores the iPhone public key.
Response confirms pairing; iPhone stores Windows public key + TLS pin in
Keychain.

## 3. Authentication

`POST /auth/challenge`

```json
{ "device_id": "<iphone-device-id>" }
```

```json
{
  "success": true,
  "challenge": "<base64url nonce>",
  "challenge_id": "<uuid>",
  "expires_at": "<ISO8601>"
}
```

iPhone: Face ID via `LAContext`, then sign:

```
signed = sign(prvKey, client_device_id ‖ challenge ‖ timestamp ‖ "/api/v1/auth/verify")
```

`POST /auth/verify`

```json
{
  "client_device_id": "<iphone-device-id>",
  "challenge_id": "<uuid>",
  "timestamp": "<ISO8601>",
  "signature": "<base64url>"
}
```

Windows verifies in order:

1. Device is paired and authorized.
2. Challenge exists, unexpired, not previously consumed (replay cache).
3. Timestamp within skew (`Security:MaxClockSkewSeconds`).
4. Signature valid against the stored iPhone public key over the exact
   canonical string.

On success:

```json
{
  "success": true,
  "session_token": "<base64url signed token>",
  "session_expires": "<ISO8601>",
  "proximity": "NEARBY"
}
```

## 4. Privileged Commands

All require `Authorization: Bearer <session_token>`.

### GET /status

```json
{
  "success": true,
  "status": {
    "locked": false,
    "battery_percent": 74,
    "proximity": "NEARBY",
    "security": "PAIRED",
    "service_version": "0.1.0"
  }
}
```

### POST /lock

```json
{ "device_id": "<iphone-device-id>" }
```

Response:

```json
{ "success": true, "message": "Laptop locked successfully" }
```

`LockWorkStation` is invoked; session lock is confirmed by the OS. Any failure
returns a structured error (e.g. `LOCK_FAILED`).

### GET /proximity

```json
{ "success": true, "proximity": "AWAY" }
```

### POST /unpair

```json
{ "device_id": "<iphone-device-id>" }
```

Removes the device's public key; future challenges for that device fail.

## 5. Canonical Signing String

To keep signatures deterministic, the canonical string is the UTF-8
concatenation of the fields with a single `\x1f` unit separator and a
single trailing `\x1e` record separator:

```
client_device_id \x1f challenge \x1f timestamp \x1f endpoint \x1e
```

CryptoKit (Ed25519/Curve25519) signs this exact byte sequence. The service
recomputes the same sequence. Curve25519 refers to Ed25519 for signatures
(`Ed25519`) and X25519 for future key exchange; the service and app both use
.NET `NSec`/BouncyCastle equivalent or `Curve25519` support as available and
documented.

## 5.5 Development-Only Token Endpoint

**Phase 2 temporary mechanism, isolated from production security.**

`GET /api/v1/dev/token` (anonymous, reachable only when the service runs in the
Development environment):

```json
{ "success": true, "data": { "token": "<runtime-random-bearer-token>" } }
```

The token is generated at startup from a CSPRNG (never hardcoded), kept in
memory, and used as `Authorization: Bearer <token>` to exercise the API until
the challenge-response protocol (Phase 4) replaces it. Outside Development the
service refuses to start while this is the only authentication provider
(fail-secure).

## 6. Error Codes

| Code | Meaning |
|------|---------|
| `AUTH_FAILED` | Signature or session invalid |
| `CHALLENGE_EXPIRED` | Challenge is too old or already used |
| `CHALLENGE_REPLAYED` | Nonce previously consumed |
| `DEVICE_UNKNOWN` | Device not paired/authorized |
| `DEVICE_UNAUTHORIZED` | Device unpaired/revoked |
| `PAIRING_INVALID` | Pairing token/signature invalid |
| `PAIRING_EXPIRED` | One-time pairing token expired |
| `LOCK_FAILED` | OS lock call failed |
| `RATE_LIMITED` | Too many requests |
| `MALFORMED_REQUEST` | Bad JSON/size/format |
| `INTERNAL_ERROR` | Unexpected failure (generic message only) |