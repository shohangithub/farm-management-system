# Farm360 AI — Enterprise Authentication & Authorization System

**Document ID:** F360-AUTH-2026-001  
**Version:** 1.0  
**Authority:** Chief Software Architect — Farm360 AI  
**Date:** July 2026  
**Governed by:** F360-CONST-2026-001 · SAD v1.0 · DDD v1.0 · F360-MTA-2026-001  
**Classification:** Confidential — Engineering Reference

---

> *"Authentication answers: Who are you? Authorization answers: What are you allowed to do? They are different questions, solved at different layers, by different mechanisms. Mixing them is the root cause of most security vulnerabilities."*

---

## Table of Contents

1. [System Overview & Design Philosophy](#1-system-overview--design-philosophy)
2. [Identity Stack Architecture](#2-identity-stack-architecture)
3. [Token Architecture](#3-token-architecture)
4. [Authentication Flows](#4-authentication-flows)
   - 4.1 Registration (Phone OTP)
   - 4.2 Login (Phone OTP)
   - 4.3 Email Verification
   - 4.4 Forgot Password
   - 4.5 Password Reset
   - 4.6 Token Refresh
   - 4.7 Logout (Single Session)
   - 4.8 Logout All Sessions
5. [Session Management](#5-session-management)
6. [Remember Device](#6-remember-device)
7. [Multi-Device Login](#7-multi-device-login)
8. [Account Lockout](#8-account-lockout)
9. [Authorization Architecture](#9-authorization-architecture)
   - 9.1 Role-Based Access Control
   - 9.2 Permission-Based Access Control
   - 9.3 Policy-Based Authorization
   - 9.4 Authorization Evaluation Pipeline
10. [Audit System](#10-audit-system)
11. [Database Design](#11-database-design)
12. [JWT Token Structure](#12-jwt-token-structure)
13. [Sequence Diagrams](#13-sequence-diagrams)
14. [Security Threat Model](#14-security-threat-model)
15. [Future Integrations](#15-future-integrations)
16. [Risk Analysis](#16-risk-analysis)
17. [Testing Strategy](#17-testing-strategy)

---

## 1. System Overview & Design Philosophy

### 1.1 Auth Stack Identity

Farm360 AI uses **phone number as the primary identity** — not email. This is a deliberate product decision based on Bangladesh context: 90%+ of target users have mobile phones; fewer than 40% have active email accounts. Email is secondary, optional, used for reports and notifications only.

```
PRIMARY AUTH:   Phone number (+880XXXXXXXXXX) + OTP
SECONDARY AUTH: Email + Password (optional, added post-registration)
FUTURE AUTH:    Social Login (Google, Facebook) · Microsoft Entra ID (NGO/Corp)
                TOTP App (Authy, Google Authenticator)
```

### 1.2 Design Pillars

| Pillar | Implementation |
|---|---|
| **Zero Password at Registration** | OTP-first; password is optional enhancement |
| **Stateless at API Layer** | JWT access tokens; no server-side session storage |
| **Stateful Refresh Tokens** | Refresh tokens are DB-backed for revocation capability |
| **Defense in Depth** | 5-layer authorization; each layer independently enforceable |
| **Non-repudiation** | Every auth event written to immutable audit log |
| **Privacy by Design** | IP addresses hashed; raw IPs never stored; PII masked in logs |
| **Tenant-Aware from Token** | TenantId embedded in JWT; no additional DB call per request |
| **Revocation in < 30 seconds** | TokenVersion mechanism ensures fast, reliable revocation |

### 1.3 Auth vs. Tenant Resolution Interaction

```
AUTHENTICATION answers: Is this a valid Farm360 user?
TENANT RESOLUTION answers: Which tenant does this user belong to right now?
AUTHORIZATION answers: What can this user do in this tenant?

Order (per Constitution §2.5 MediatR Pipeline):
  [1] JWT Authentication  → validates WHO
  [2] TenantResolutionMiddleware → validates WHICH tenant + subscription status
  [3] Authorization policies → validates WHAT they can do
  [4] Farm ABAC check     → validates WHICH specific resources
  [5] Domain rules        → validates IF the entity allows the action
```

---

## 2. Identity Stack Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                     IDENTITY STACK LAYERS                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  LAYER 1: ASP.NET Core Identity                                         │
│  ─────────────────────────────                                          │
│  Manages: ApplicationUser entity · Password hashing (PBKDF2/SHA256)     │
│  Manages: User claims · User roles · Security stamp                     │
│  Does NOT manage: JWT · OTP · Refresh tokens · Multi-tenant             │
│  Database: identity.* schema (separate from business data)              │
│                                                                         │
│  LAYER 2: JWT Token Service                                             │
│  ──────────────────────────                                             │
│  Algorithm: RS256 (asymmetric — private key signs, public key verifies) │
│  Key storage: AWS KMS (HSM-backed; private key never leaves KMS)        │
│  Access token: 15-minute expiry (balance: security vs. UX)              │
│  Distribution: Authorization Bearer header                              │
│  Revocation: TokenVersion claim checked per-request                     │
│                                                                         │
│  LAYER 3: Refresh Token Service                                         │
│  ─────────────────────────────                                          │
│  Token: 256-bit cryptographically random opaque string                  │
│  Storage: HMAC-SHA256 hash stored in identity.UserSessions              │
│  Expiry: 30 days (rolling window — reset on each use)                  │
│  Rotation: strict one-time use (reuse = immediate full revocation)      │
│  Binding: DeviceFingerprint ensures token can only be used by issuing   │
│           device (optional — see §6)                                    │
│                                                                         │
│  LAYER 4: OTP Service                                                   │
│  ────────────────────                                                   │
│  OTP: 6-digit numeric                                                   │
│  Storage: HMAC-SHA256 hash in Redis (TenantId:otp:{phone}:{purpose})   │
│  Expiry: 10 minutes                                                     │
│  Attempts: max 3 per OTP → 30-minute phone lockout                     │
│  Rate limit: max 5 OTP requests per phone per hour                     │
│  OTP value: NEVER logged (Serilog masking policy)                       │
│                                                                         │
│  LAYER 5: Authorization Service                                         │
│  ─────────────────────────────                                          │
│  RBAC: role claim in JWT → role-to-permission map                      │
│  PBAC: permission claims evaluated per endpoint policy                  │
│  ABAC: farm assignment evaluated in handler                             │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.1 ApplicationUser Entity

```
identity.Users (extends ASP.NET Identity IdentityUser)
  Id:                UNIQUEIDENTIFIER  (PK — same as platform.Users.Id)
  PhoneNumber:       NVARCHAR(20)      NOT NULL, UNIQUE (+880XXXXXXXXXX)
  PhoneConfirmed:    BIT               NOT NULL, DEFAULT 0
  Email:             NVARCHAR(256)     NULL (optional)
  EmailConfirmed:    BIT               NOT NULL, DEFAULT 0
  PasswordHash:      NVARCHAR(MAX)     NULL (optional — OTP-first system)
  SecurityStamp:     NVARCHAR(MAX)     (rotated on security-sensitive changes)
  TokenVersion:      INT               NOT NULL, DEFAULT 1 (increment=revoke all)
  LockoutEnabled:    BIT               NOT NULL, DEFAULT 1
  LockoutEnd:        DATETIMEOFFSET    NULL
  AccessFailedCount: INT               NOT NULL, DEFAULT 0
  TwoFactorEnabled:  BIT               NOT NULL, DEFAULT 0
  IsSystemUser:      BIT               NOT NULL, DEFAULT 0
  CreatedAt:         DATETIME2(7)      NOT NULL
  LastLoginAt:       DATETIME2(7)      NULL
  LastLoginIpHash:   NVARCHAR(64)      NULL (HMAC-SHA256 of IP)
```

### 2.2 Key Management Architecture

```
JWT Signing Key Lifecycle:

  [1] Key stored in AWS KMS (RSA 2048-bit, HSM-backed)
  [2] Farm360.Identity → AWSSDK.KMS → Sign payload (KMS never returns raw key)
  [3] Public key distributed via: GET /.well-known/jwks.json
  [4] Angular and all API consumers validate JWT using public key only
  [5] Key rotation: every 90 days
  [6] Rotation strategy: dual-key 30-day overlap (old key still validates old tokens)
  [7] Rotation triggers: scheduled · manual · security incident response

JWKS Endpoint:
  URL: GET /.well-known/jwks.json
  Cache-Control: max-age=3600 (1 hour)
  Format: RFC 7517 JSON Web Key Set
  Public: YES (safe — contains only public key)
  Angular: bootstraps by fetching this at startup
```

---

## 3. Token Architecture

### 3.1 Access Token

```
Algorithm:  RS256
Expiry:     15 minutes (PT15M)
Size:       ~800 bytes (keep lean — hits every API call)

JWT Header:
{
  "alg": "RS256",
  "typ": "JWT",
  "kid": "farm360-key-v3"        ← key version for rotation
}

JWT Payload:
{
  "iss": "https://auth.farm360.ai",
  "aud": "https://api.farm360.ai",
  "sub": "{userId}",              ← ApplicationUser.Id
  "jti": "{uuid}",                ← JWT ID (unique per token)
  "iat": 1720310400,
  "exp": 1720311300,              ← iat + 900 (15 min)
  "tenant_id": "{tenantId}",
  "tenant_slug": "green-valley",
  "role": "FarmManager",          ← single role (OrganizationUser.Role)
  "farms": ["farmId1","farmId2"], ← assigned farm IDs (null = all)
  "tv": 5,                        ← token_version (revocation counter)
  "tier": "Khamar"                ← subscription tier
}
```

### 3.2 Refresh Token

```
Format:      256-bit Base64url encoded random string (NOT a JWT)
Storage:     HMAC-SHA256 hash in identity.UserSessions
Expiry:      30 days (rolling — reset on every successful use)
Rotation:    One-time use; each use issues new token + invalidates old
Binding:     DeviceFingerprint (SHA256) bound at issuance
Reuse:       Refresh token reuse = theft indicator
             → Immediate: revoke ALL sessions for that user
             → Alert: send security SMS to user
             → Audit: AuthEvent.SuspiciousRefreshTokenReuse
```

### 3.3 Device Token (Remember Device)

```
Format:      256-bit Base64url encoded random string
Storage:     HMAC-SHA256 hash in identity.UserDevices
Expiry:      90 days (NOT rolling — fixed from issuance)
Purpose:     Allows OTP skip on recognized device for 90 days
Cookie:      HttpOnly, Secure, SameSite=Strict, Path=/api/v1/auth
             Name: farm360_dt
             Domain: .farm360.ai
Invalidation: user-initiated device removal · password change · account lock
```

### 3.4 Token Lifecycle State Machine

```
                    ┌─────────────────┐
                    │    ISSUED        │
                    └───────┬─────────┘
                            │ Used before expiry
              ┌─────────────┼─────────────┐
              ▼             ▼             ▼
        ┌──────────┐  ┌──────────┐  ┌──────────┐
        │ REFRESHED│  │ REVOKED  │  │ EXPIRED  │
        │ (new     │  │ (manual  │  │ (15min/  │
        │ token    │  │ logout,  │  │ 30d)     │
        │ issued)  │  │ pw change│  │          │
        └──────────┘  └──────────┘  └──────────┘
              │                          │
              └──────────────────────────┘
                     Both → 401
```

---

## 4. Authentication Flows

### 4.1 Registration Flow (Phone OTP — Primary)

```
PRECONDITIONS: Phone not already registered

STEP 1 — Initiate Registration
  POST /api/v1/auth/register/initiate
  Body: { phone, fullName, referralCode? }

  Server:
  [a] Validate phone format: +880[6-9][0-9]{8} (regex)
  [b] Check phone not already registered → 409 if exists
  [c] Check rate limit: max 5 OTP per phone per hour (Redis)
  [d] Generate 6-digit OTP
  [e] Store: Redis.Set("otp:register:{phone}", HMAC(OTP), TTL=10min)
  [f] Store: Redis.Set("otp:register:meta:{phone}",
             { attempts:0, name:fullName }, TTL=10min)
  [g] Send SMS via ISmsService
  [h] Return: 200 { maskedPhone: "+880171XXXX05", expiresInSeconds: 600 }

  Audit: AUTH_OTP_SENT {phone(masked), purpose=REGISTRATION}

STEP 2 — Verify OTP + Complete Registration
  POST /api/v1/auth/register/verify
  Body: { phone, otp, organizationName, timezone?, language? }

  Server:
  [a] Retrieve OTP hash from Redis
  [b] Increment attempt counter → 409 if attempts >= 3
  [c] Compare HMAC(submitted_otp) == stored_hash → 422 if mismatch
  [d] Delete OTP from Redis (consumed)
  [e] BEGIN TRANSACTION
      → INSERT platform.Tenants (new tenant for this organization)
      → INSERT platform.Organizations
      → INSERT identity.Users (ApplicationUser)
      → INSERT platform.Users (business user)
      → INSERT platform.OrganizationUsers (role=Owner)
      → INSERT platform.Subscriptions (tier=Bittho, trial=14 days)
  [f] COMMIT
  [g] Mark PhoneConfirmed=true
  [h] Issue: AccessToken + RefreshToken + DeviceId
  [i] Return: 201 { accessToken, refreshToken, expiresIn, tenantSlug }

  Audit: AUTH_REGISTRATION_COMPLETE {userId, tenantId, phone(masked)}
  Event: TenantRegisteredEvent (→ sends welcome SMS, seeds default data)

FAILURE MODES:
  Phone already registered → 409 Conflict
  OTP expired             → 422 (Redis key gone) → "OTP expired, request new"
  OTP mismatch (attempt 1,2) → 422 + "N attempts remaining"
  OTP mismatch (attempt 3)   → 422 + 30-min lockout
  Phone rate limited         → 429 + "Too many OTP requests"
```

### 4.2 Login Flow (Phone OTP — Primary)

```
PRECONDITIONS: Account exists, not locked, not suspended

STEP 1 — Initiate Login
  POST /api/v1/auth/login/initiate
  Body: { phone }
  Headers: X-Device-Fingerprint: {sha256 of UA+screen+timezone}

  Server:
  [a] Normalize phone to +880 format
  [b] Lookup user by phone → 404 if not found (→ "Account not found")
  [c] Check LockoutEnd → 423 if locked (return LockoutEnd timestamp)
  [d] Check tenant status → 402 if suspended
  [e] Check "Remember Device" cookie → if valid device token:
       → Skip OTP entirely → go to STEP 2B (direct token issuance)
  [f] Rate limit: max 5 OTP/phone/hour → 429 if exceeded
  [g] Generate OTP → Redis.Set("otp:login:{phone}", HMAC(OTP), TTL=10min)
  [h] Send SMS
  [i] Return: 200 { maskedPhone, expiresInSeconds:600, requiresOtp:true }

STEP 2A — Verify OTP (standard path)
  POST /api/v1/auth/login/verify
  Body: { phone, otp, rememberDevice? }

  Server:
  [a] Retrieve OTP hash → 422 if expired
  [b] Validate OTP → 422 if mismatch; increment failCount
  [c] If failCount >= 3: LockoutEnd = NOW + 30min → 423
  [d] Delete OTP from Redis
  [e] Issue tokens (see §3)
  [f] If rememberDevice=true: issue device token (see §6)
  [g] Create identity.UserSessions record
  [h] Update LastLoginAt, LastLoginIpHash
  [i] Return: 200 { accessToken, refreshToken, expiresIn, sessionId }

STEP 2B — Trusted Device (skip OTP)
  Server:
  [a] Validate device token hash against identity.UserDevices
  [b] Assert device not expired, not revoked
  [c] Assert device is bound to same user/phone
  [d] Issue tokens directly
  [e] Return: 200 { accessToken, refreshToken, expiresIn, sessionId }

  Audit: AUTH_LOGIN_SUCCESS {userId, tenantId, deviceId, ipHash, method}

LOGIN WITH PASSWORD (Optional — secondary path for power users)
  POST /api/v1/auth/login/password
  Body: { phone, password }

  Server:
  [a] Lookup user → verify PasswordHash (PBKDF2)
  [b] Check lockout, check suspension
  [c] If 2FA enabled → initiate TOTP/OTP verification
  [d] Issue tokens on success
  [e] Increment AccessFailedCount on failure; lock at 5 failures
```

### 4.3 Email Verification Flow

```
TRIGGER: User adds email to profile (optional post-registration step)
         OR invitation accepted via email link

POST /api/v1/auth/email/send-verification
  Body: { email }
  Auth: Bearer (authenticated user)

  Server:
  [a] Validate email format
  [b] Assert email not already used by another account
  [c] Generate: VerificationToken = URL-safe Base64(GUID + timestamp)
  [d] Store: Redis.Set("email-verify:{userId}", HMAC(token), TTL=24h)
  [e] Send via IEmailService: link = https://app.farm360.ai/verify-email?token=X
  [f] Return: 200 { "Verification email sent" }

GET /api/v1/auth/email/verify?token={token}
  [a] Decode token → extract userId
  [b] Retrieve hash from Redis → 422 if expired
  [c] Compare hashes → 422 if mismatch
  [d] Set EmailConfirmed=true
  [e] Delete from Redis
  [f] Redirect: https://app.farm360.ai/email-verified (Angular handles UI)

  Audit: AUTH_EMAIL_VERIFIED {userId, email(masked)}
```

### 4.4 Forgot Password Flow

```
POST /api/v1/auth/password/forgot
  Body: { phone }

  Server:
  [a] Lookup user by phone → always return 200 (no account enumeration)
  [b] If user exists AND phone confirmed:
      → Generate OTP
      → Redis.Set("otp:password-reset:{phone}", HMAC(OTP), TTL=10min)
      → Send SMS: "Your Farm360 password reset code: XXXXXX (10 min)"
  [c] Return: 200 { "If your phone is registered, you will receive an OTP" }

  Audit: AUTH_PASSWORD_RESET_INITIATED {phone(masked)} [user existence NOT logged]
```

### 4.5 Password Reset Flow

```
STEP 1 — Verify Reset OTP
  POST /api/v1/auth/password/reset/verify-otp
  Body: { phone, otp }

  Server:
  [a] Validate OTP (same mechanism as login)
  [b] On success: generate short-lived reset token (5-minute TTL)
      → Redis.Set("pwd-reset-token:{phone}", HMAC(resetToken), TTL=5min)
  [c] Return: 200 { resetToken, expiresInSeconds:300 }

STEP 2 — Set New Password
  POST /api/v1/auth/password/reset
  Body: { phone, resetToken, newPassword, confirmPassword }

  Server:
  [a] Validate resetToken from Redis → 422 if expired
  [b] Validate password strength:
      → Min 8 characters
      → At least 1 uppercase, 1 lowercase, 1 digit, 1 special
      → Not same as last 5 passwords (hash history)
  [c] Set new PasswordHash
  [d] Rotate SecurityStamp (invalidates any active cookies)
  [e] Increment TokenVersion (revokes ALL active JWT sessions)
  [f] Delete all UserSessions (revokes all refresh tokens)
  [g] Delete resetToken from Redis
  [h] Send SMS: "Your Farm360 password was changed. Not you? Call XXXXXXXX"
  [i] Return: 200 { "Password reset successful. Please log in." }

  Audit: AUTH_PASSWORD_RESET_COMPLETE {userId, phone(masked), allSessionsRevoked:true}
```

### 4.6 Token Refresh Flow

```
POST /api/v1/auth/token/refresh
  Body: { refreshToken }
  Headers: X-Device-Fingerprint: {fingerprint}
  Cookie: farm360_dt (device token, if present)

  Server:
  [a] Hash submitted refresh token
  [b] Lookup in identity.UserSessions WHERE TokenHash={hash}
      → 401 "Invalid token" if not found (generic — no details)
  [c] Check session IsRevoked → 401 if revoked
  [d] Check session ExpiresAt → 401 if expired
  [e] Validate DeviceFingerprint matches session record
      → Mismatch: log warning; do NOT revoke (fingerprint can change with browser updates)
  [f] *** ROTATION — CRITICAL STEP ***
      → Mark old session IsRevoked=true
      → Generate new refresh token
      → Create new UserSessions record
      → Link: NewSession.ReplacedBySessionId = OldSession.Id
  [g] Issue new access token (read fresh role/farms from DB)
  [h] Return: 200 { accessToken, refreshToken, expiresIn }

  *** REUSE ATTACK DETECTION ***
  If lookup in [b] finds a REVOKED session:
  [i] This is a refresh token reuse attempt (possible theft scenario)
  [j] Identify the session family (follow chain from root)
  [k] Revoke ALL sessions in that family (cascade via ReplacedBySessionId chain)
  [l] Increment TokenVersion (invalidates ALL active access tokens)
  [m] Send SMS: "Security alert: unusual login detected on your Farm360 account"
  [n] Audit: AUTH_SUSPICIOUS_TOKEN_REUSE (high severity)
  [o] Return: 401 "Session invalid"

  Audit: AUTH_TOKEN_REFRESHED {sessionId, userId, tenantId}
```

### 4.7 Logout (Single Session)

```
POST /api/v1/auth/logout
  Auth: Bearer
  Body: { refreshToken }

  Server:
  [a] Validate access token (must be valid)
  [b] Find matching UserSession → mark IsRevoked=true
  [c] Clear device token cookie (Set-Cookie: farm360_dt=; expires=past)
  [d] Return: 204 No Content

  Note: Access token cannot be revoked (it's stateless)
        Client MUST discard it; it expires naturally in ≤15 min
        Security window: 15 min max for access token after logout
  
  Audit: AUTH_LOGOUT {sessionId, userId}
```

### 4.8 Logout All Sessions

```
POST /api/v1/auth/logout/all
  Auth: Bearer

  Server:
  [a] Increment TokenVersion → invalidates ALL existing access tokens
  [b] Mark ALL UserSessions IsRevoked=true for this user
  [c] Delete ALL UserDevices records (removes remember-device on all devices)
  [d] Return: 204 No Content

  Audit: AUTH_LOGOUT_ALL_SESSIONS {userId, sessionCount}
```

---

## 5. Session Management

### 5.1 Session Object

```
identity.UserSessions
  Id:                 UNIQUEIDENTIFIER  PK
  UserId:             UNIQUEIDENTIFIER  FK → identity.Users(Id)
  TokenHash:          NVARCHAR(128)     HMAC-SHA256 of refresh token
  DeviceId:           UNIQUEIDENTIFIER  FK → identity.UserDevices(Id) NULL
  DeviceName:         NVARCHAR(256)     "Chrome on Windows 11" (parsed from UA)
  DeviceType:         TINYINT           0=Browser, 1=MobileApp, 2=DesktopApp
  DeviceFingerprint:  NVARCHAR(64)      SHA256 of browser fingerprint
  IpHash:             NVARCHAR(64)      HMAC-SHA256 of IP address
  UserAgent:          NVARCHAR(512)     raw (for display only)
  Location:           NVARCHAR(100)     "Dhaka, BD" (GeoIP — approximate)
  IssuedAt:           DATETIME2(7)
  ExpiresAt:          DATETIME2(7)      IssuedAt + 30 days
  LastUsedAt:         DATETIME2(7)
  IsRevoked:          BIT               DEFAULT 0
  RevokedAt:          DATETIME2(7)      NULL
  RevokedReason:      TINYINT           NULL (0=Logout,1=PasswordChange,2=Admin,3=Suspicious)
  ReplacedBySessionId:UNIQUEIDENTIFIER  NULL (rotation chain)
  TenantId:           UNIQUEIDENTIFIER  FK (for filtering)
```

### 5.2 Session List Endpoint

```
GET /api/v1/auth/sessions
  Auth: Bearer
  
  Returns: Active sessions for current user
  [
    {
      "sessionId": "...",
      "deviceName": "Chrome on Windows 11",
      "deviceType": "Browser",
      "location": "Dhaka, BD",
      "issuedAt": "2026-07-01T10:00:00Z",
      "lastUsedAt": "2026-07-07T06:00:00Z",
      "isCurrent": true
    }
  ]

DELETE /api/v1/auth/sessions/{sessionId}
  Auth: Bearer
  → Revoke specific session (not current)
  → RevokedReason = UserInitiated
```

### 5.3 Session Cleanup

```
Hangfire Job: SessionCleanupJob (daily 01:00 BDT)
  → DELETE WHERE IsRevoked=1 AND RevokedAt < NOW - 30 days
  → DELETE WHERE ExpiresAt < NOW AND IsRevoked=0 (mark revoked first)
```

---

## 6. Remember Device

### 6.1 Device Registration Flow

```
TRIGGERED: User selects "Remember this device" at login

Server:
[1] Generate: deviceToken = 256-bit Base64url random
[2] Generate: deviceId = new GUID
[3] Store in identity.UserDevices:
    { Id=deviceId, UserId, DeviceFingerprint, TokenHash=HMAC(deviceToken),
      DeviceName (parsed UA), IssuedAt, ExpiresAt=NOW+90d, IsRevoked=false }
[4] Set HttpOnly cookie:
    Set-Cookie: farm360_dt={deviceId}:{deviceToken}; HttpOnly; Secure;
                SameSite=Strict; Path=/api/v1/auth; Max-Age=7776000
    (7,776,000 seconds = 90 days)
[5] Return session as normal (no change to response body)
```

### 6.2 Trusted Device Validation

```
On every login initiate:
[1] Read farm360_dt cookie → extract deviceId + deviceToken
[2] Lookup identity.UserDevices WHERE Id=deviceId AND UserId=userId
[3] Validate: HMAC(deviceToken) == TokenHash
[4] Validate: ExpiresAt > NOW
[5] Validate: IsRevoked == false
[6] Validate: UserId matches submitting user (cross-user cookie impossible)
→ ALL checks pass: Skip OTP; issue tokens directly
→ ANY check fails: Ignore cookie; require OTP; do NOT revoke (may be cookie corruption)

Security Note:
  Device tokens are scoped to /api/v1/auth path only (cannot be sent to other endpoints)
  90-day fixed expiry (not rolling) — user must re-authenticate periodically
```

### 6.3 Device List Management

```
GET /api/v1/auth/devices
  Auth: Bearer
  Returns: List of registered devices with name, type, lastUsed, isTrusted

DELETE /api/v1/auth/devices/{deviceId}
  Auth: Bearer
  → Marks IsRevoked=true
  → Clears cookie on response if deviceId matches current
  Audit: AUTH_DEVICE_REMOVED {deviceId, deviceName}

DELETE /api/v1/auth/devices (remove all)
  → Revokes all trusted devices for user
  Audit: AUTH_ALL_DEVICES_REMOVED {userId, count}
```

---

## 7. Multi-Device Login

### 7.1 Design Principle

```
Farm360 AI allows CONCURRENT login from multiple devices simultaneously.
There is NO single-session enforcement.

Rationale:
  → Owner checks dashboard on phone while Worker logs consumption on tablet
  → Accountant works on desktop while owner views on mobile
  → This is a feature, not a security hole — managed via session visibility

Limits by Tier:
  Bittho:   2 concurrent active sessions
  Khamar:   5 concurrent active sessions
  Banik:    10 concurrent active sessions
  Corp/NGO: Unlimited
```

### 7.2 Session Limit Enforcement

```
On STEP 2 of login (before issuing new tokens):
[1] Count active sessions for user: SELECT COUNT WHERE IsRevoked=0 AND ExpiresAt > NOW
[2] If count >= MaxSessionsForTier:
    → Revoke OLDEST session (by LastUsedAt ASC)
    → Notify that device via SignalR: SESSION_REVOKED event
    → Then proceed to issue new session
```

### 7.3 Cross-Device Security Notifications

```
On login from new device:
  → Send SMS to registered phone: "New login: Chrome on Windows, Dhaka 07/07 10:30 AM
    Not you? Secure your account: farm360.ai/security"

On login from unrecognized location (GeoIP mismatch > 500km from usual):
  → Add flag: IsHighRisk=true on session
  → Require OTP even if device is trusted
  → Alert: "Unusual location login detected"
```

---

## 8. Account Lockout

### 8.1 Lockout Policy (Progressive)

```
TRIGGER: Failed OTP verifications OR failed password attempts

OTP Failures (Login + Password Reset):
  Attempt 1 (fail): Warning message + 2 attempts remaining
  Attempt 2 (fail): Warning message + 1 attempt remaining
  Attempt 3 (fail): Phone locked for 30 minutes
                    SMS: "Account temporarily locked. Try again at [time]"
                    Audit: AUTH_ACCOUNT_LOCKED {phone(masked), lockDuration:30min}

Password Failures:
  1–3 failures: Warning + count shown
  4 failures:   Warning + "1 attempt before lockout"
  5 failures:   Account locked for 60 minutes
  Repeat:       Each 5-failure cycle doubles lockout: 60→120→240→480 min→permanent
  Permanent:    Requires Platform Admin unlock or owner re-verification

LOCKOUT SCOPE:
  OTP lockout:   Per phone number (Redis key: "lockout:phone:{phone}")
  Password:      Per user account (ASP.NET Identity LockoutEnd field)
  Both:          Return 423 Locked with Retry-After header
```

### 8.2 Lockout State Response

```
HTTP 423 Locked
{
  "type": "https://farm360.ai/errors/account-locked",
  "title": "Account Temporarily Locked",
  "status": 423,
  "detail": "Too many failed attempts. Account locked until 2026-07-07T07:30:00Z",
  "retryAfter": "2026-07-07T07:30:00Z",
  "retryAfterSeconds": 1800,
  "correlationId": "req-12345"
}
```

### 8.3 Admin Unlock Flow

```
Platform Admin:
  POST /api/v1/admin/users/{userId}/unlock
  → Reset AccessFailedCount to 0
  → Set LockoutEnd = null
  → Does NOT issue new tokens (user must re-authenticate)
  Audit: AUTH_ACCOUNT_UNLOCKED_BY_ADMIN {adminUserId, targetUserId, reason}

Self-Unlock (after lockout period expires):
  → Automatic — LockoutEnd passes; next login attempt proceeds normally
```

---

## 9. Authorization Architecture

### 9.1 Role-Based Access Control (RBAC)

```
Roles (defined in UserRole enum):
  Owner         (0) — Full access to all tenant data and settings
  FarmManager   (1) — Operational access; cannot manage users or finance
  Veterinarian  (2) — Health module full; read-only elsewhere
  Worker        (3) — Data entry for assigned farms; no financial or admin
  Accountant    (4) — Finance module full; read-only elsewhere
  Viewer        (5) — Read-only across all modules for assigned farms

Source: OrganizationUser.Role
Embedded in: JWT role claim (read per-request from token — no DB call)
Change propagation: Role change → TokenVersion++ → re-login required
```

### 9.2 Permission-Based Access Control (PBAC)

```
PERMISSION REGISTRY — Resource:Action pairs

animals:read          animals:write         animals:delete
animals:transfer      animals:sell          animals:quarantine
batches:read          batches:write
farms:read            farms:write           farms:manage
feeding:read          feeding:write         feeding:schedule
health:read           health:write          health:protocol
inventory:read        inventory:write       inventory:adjust
finance:read          finance:write         finance:close-period
reports:read          reports:export
users:read            users:invite          users:manage
settings:read         settings:write
notifications:read
audit:read
subscription:manage
platform:admin

ROLE → PERMISSION MAPPING (compile-time constant — PermissionRegistry.cs)

Owner:       ALL permissions
FarmManager: animals:* · batches:* · farms:read · feeding:* · health:* ·
             inventory:* · reports:read · reports:export · notifications:read
Vet:         health:* · animals:read · batches:read · reports:read
Worker:      animals:read · animals:write · feeding:read · feeding:write ·
             health:read · inventory:read · notifications:read
Accountant:  finance:* · reports:* · animals:read · inventory:read ·
             audit:read · notifications:read
Viewer:      animals:read · batches:read · farms:read · feeding:read ·
             health:read · inventory:read · finance:read · reports:read
```

### 9.3 Policy-Based Authorization (ASP.NET Core)

```
POLICY DEFINITIONS (registered at startup)

SIMPLE ROLE POLICIES:
  "IsOwner"         → Role == Owner
  "IsOwnerOrAdmin"  → Role == Owner || FarmManager

PERMISSION POLICIES (one per permission string):
  "animals:read"    → user has animals:read in their role's permissions
  "finance:write"   → user has finance:write
  ...all 30 permissions registered as policies

COMPOSITE POLICIES (multi-requirement):
  "FinancePeriodClose"  → Role==Owner AND finance:close-period AND farm:owned
  "UserManagement"      → (Role==Owner OR FarmManager) AND users:manage
  "PlatformAdmin"       → IsSystemUser==true (internal only)
  "EnterpriseOnly"      → Tier==Corporation AND animals:read
  "GracePeriodReadOnly" → TenantStatus==GracePeriod → GET methods only

ENDPOINT APPLICATION:
  Every Minimal API endpoint declares:
  .RequireAuthorization("animals:write")
  or
  .RequireAuthorization(p => p.RequireRole("Owner", "FarmManager"))
```

### 9.4 Authorization Evaluation Pipeline (5 Layers)

```
EVERY REQUEST goes through all applicable layers in order:

┌─────────────────────────────────────────────────────────────────────┐
│ LAYER 1: JWT Validation (Middleware)                                 │
│   → Is the token cryptographically valid?                           │
│   → Is it expired?                                                  │
│   → Is TokenVersion current? (revocation check)                    │
│   FAIL → 401 Unauthorized                                           │
├─────────────────────────────────────────────────────────────────────┤
│ LAYER 2: Tenant Validation (TenantResolutionMiddleware)             │
│   → Does this tenant exist and is it Active/GracePeriod?           │
│   → Does subscription allow this operation?                         │
│   FAIL → 402 (Suspended) or 404 (Deleted)                          │
├─────────────────────────────────────────────────────────────────────┤
│ LAYER 3: Policy Authorization (ASP.NET Core IAuthorizationService)  │
│   → Does user's role include the required permission?               │
│   → Do composite policy requirements pass?                          │
│   FAIL → 403 Forbidden (generic — no detail on why)                │
├─────────────────────────────────────────────────────────────────────┤
│ LAYER 4: ABAC — Farm Scope (Application Handler)                    │
│   → Is the requested farm in user's AssignedFarmIds?               │
│   → For null AssignedFarmIds: all farms permitted                  │
│   FAIL → 404 (not 403 — do not confirm resource existence)         │
├─────────────────────────────────────────────────────────────────────┤
│ LAYER 5: Domain Rules (Domain Entity)                               │
│   → Is the entity in a state that allows this action?              │
│   → E.g.: Can quarantined animal be sold?                           │
│   FAIL → 422 Unprocessable + DomainException message               │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 10. Audit System

### 10.1 Auth Audit Log

```
identity.AuthAuditLogs (INSERT ONLY — no UPDATE, DELETE, soft delete)

  Id:              UNIQUEIDENTIFIER  PK
  EventType:       NVARCHAR(64)      See Event Catalog below
  Severity:        TINYINT           0=Info, 1=Warning, 2=Alert, 3=Critical
  UserId:          UNIQUEIDENTIFIER  NULL (before authentication)
  TenantId:        UNIQUEIDENTIFIER  NULL (before resolution)
  PhoneMasked:     NVARCHAR(20)      NULL  +880171XXXX05
  EmailMasked:     NVARCHAR(64)      NULL  r****@gmail.com
  IpHash:          NVARCHAR(64)      HMAC-SHA256(IP, secret)
  UserAgent:       NVARCHAR(512)
  DeviceId:        UNIQUEIDENTIFIER  NULL
  SessionId:       UNIQUEIDENTIFIER  NULL
  Location:        NVARCHAR(100)     "Dhaka, BD" (GeoIP)
  CorrelationId:   NVARCHAR(64)      X-Correlation-Id header
  AdditionalData:  NVARCHAR(MAX)     JSON — event-specific metadata
  OccurredAt:      DATETIME2(7)      NOT NULL
  
INDEX: IX_AuthAuditLogs_TenantId_OccurredAt (TenantId, OccurredAt DESC)
INCLUDE: EventType, Severity, UserId, SessionId
Partition: Monthly partition on OccurredAt
```

### 10.2 Auth Event Catalog

| EventType | Severity | Trigger |
|---|---|---|
| `AUTH_OTP_SENT` | Info | OTP SMS dispatched |
| `AUTH_OTP_VERIFIED` | Info | OTP successfully validated |
| `AUTH_OTP_FAILED` | Warning | Wrong OTP submitted |
| `AUTH_REGISTRATION_COMPLETE` | Info | New user registered |
| `AUTH_LOGIN_SUCCESS` | Info | Successful login |
| `AUTH_LOGIN_FAILED` | Warning | Failed login attempt |
| `AUTH_TOKEN_REFRESHED` | Info | Refresh token rotated |
| `AUTH_LOGOUT` | Info | Single session logout |
| `AUTH_LOGOUT_ALL` | Info | All sessions revoked |
| `AUTH_PASSWORD_RESET_INITIATED` | Info | Password reset OTP sent |
| `AUTH_PASSWORD_RESET_COMPLETE` | Info | Password successfully reset |
| `AUTH_EMAIL_VERIFIED` | Info | Email confirmed |
| `AUTH_ACCOUNT_LOCKED` | Warning | Account locked after failures |
| `AUTH_ACCOUNT_UNLOCKED_BY_ADMIN` | Warning | Admin unlocked account |
| `AUTH_DEVICE_REGISTERED` | Info | Device added to trusted devices |
| `AUTH_DEVICE_REMOVED` | Info | Device removed |
| `AUTH_SUSPICIOUS_TOKEN_REUSE` | Critical | Refresh token used after revocation |
| `AUTH_SUSPICIOUS_LOCATION` | Alert | Login from unusual location |
| `AUTH_TOKEN_VERSION_MISMATCH` | Alert | Revoked token used |
| `AUTH_ROLE_CHANGED` | Warning | User role modified |
| `AUTH_RATE_LIMIT_HIT` | Warning | OTP rate limit exceeded |
| `AUTH_BRUTE_FORCE_SUSPECTED` | Critical | Excessive failures from same IP |

### 10.3 Audit Retention & Access

```
Retention: 7 years (financial compliance) for all auth events
Access:    
  → Owner/FarmManager: own account audit events only (GET /api/v1/auth/audit)
  → Platform Admin: all events across all tenants
  → Raw IP hashes: accessible only to Platform Admin with KMS decrypt

Security Alerts:
  → Severity=Critical events: immediate SNS notification to Platform Admin
  → 5+ Severity=Warning events from same IP in 60 seconds: block at WAF
```

---

## 11. Database Design

### 11.1 Complete Auth Schema

```
═══════════════════════════════════════════════════════════════════
identity.Users  (ASP.NET Identity — extends IdentityUser)
═══════════════════════════════════════════════════════════════════
  Id                    UNIQUEIDENTIFIER    PK
  PhoneNumber           NVARCHAR(20)        NOT NULL, UNIQUE
  PhoneNumberConfirmed  BIT                 NOT NULL DEFAULT 0
  Email                 NVARCHAR(256)       NULL
  EmailConfirmed        BIT                 NOT NULL DEFAULT 0
  PasswordHash          NVARCHAR(MAX)       NULL
  SecurityStamp         NVARCHAR(MAX)       NOT NULL
  ConcurrencyStamp      NVARCHAR(MAX)
  TokenVersion          INT                 NOT NULL DEFAULT 1
  LockoutEnabled        BIT                 NOT NULL DEFAULT 1
  LockoutEnd            DATETIMEOFFSET(7)   NULL
  AccessFailedCount     INT                 NOT NULL DEFAULT 0
  TwoFactorEnabled      BIT                 NOT NULL DEFAULT 0
  TotpSecretEncrypted   NVARCHAR(MAX)       NULL (KMS-encrypted TOTP secret)
  IsSystemUser          BIT                 NOT NULL DEFAULT 0
  CreatedAt             DATETIME2(7)        NOT NULL
  LastLoginAt           DATETIME2(7)        NULL
  LastLoginIpHash       NVARCHAR(64)        NULL
  PasswordHistory       NVARCHAR(MAX)       NULL (JSON: last 5 hashes)

═══════════════════════════════════════════════════════════════════
identity.UserSessions  (Refresh token store)
═══════════════════════════════════════════════════════════════════
  Id                    UNIQUEIDENTIFIER    PK
  UserId                UNIQUEIDENTIFIER    FK → identity.Users(Id)
  TenantId              UNIQUEIDENTIFIER    NOT NULL (for filtering)
  TokenHash             NVARCHAR(128)       NOT NULL UNIQUE
  DeviceId              UNIQUEIDENTIFIER    FK → identity.UserDevices(Id) NULL
  DeviceName            NVARCHAR(256)
  DeviceType            TINYINT             (0=Browser, 1=Mobile, 2=Desktop)
  DeviceFingerprint     NVARCHAR(64)
  IpHash                NVARCHAR(64)
  UserAgent             NVARCHAR(512)
  Location              NVARCHAR(100)
  IsHighRisk            BIT                 DEFAULT 0
  IssuedAt              DATETIME2(7)        NOT NULL
  ExpiresAt             DATETIME2(7)        NOT NULL
  LastUsedAt            DATETIME2(7)        NOT NULL
  IsRevoked             BIT                 NOT NULL DEFAULT 0
  RevokedAt             DATETIME2(7)        NULL
  RevokedReason         TINYINT             NULL
  ReplacedBySessionId   UNIQUEIDENTIFIER    NULL (rotation chain)
  INDEX: IX_UserSessions_UserId_IsRevoked (UserId, IsRevoked) WHERE IsRevoked=0
  INDEX: IX_UserSessions_TokenHash (TokenHash) — covered for lookup

═══════════════════════════════════════════════════════════════════
identity.UserDevices  (Remember device store)
═══════════════════════════════════════════════════════════════════
  Id                    UNIQUEIDENTIFIER    PK
  UserId                UNIQUEIDENTIFIER    FK → identity.Users(Id)
  TokenHash             NVARCHAR(128)       NOT NULL UNIQUE
  DeviceFingerprint     NVARCHAR(64)        NOT NULL
  DeviceName            NVARCHAR(256)
  DeviceType            TINYINT
  IssuedAt              DATETIME2(7)        NOT NULL
  ExpiresAt             DATETIME2(7)        NOT NULL
  LastUsedAt            DATETIME2(7)        NOT NULL
  IsRevoked             BIT                 NOT NULL DEFAULT 0
  INDEX: IX_UserDevices_UserId (UserId) WHERE IsRevoked=0

═══════════════════════════════════════════════════════════════════
identity.OtpVerifications  (Audit trail — Redis is primary store)
═══════════════════════════════════════════════════════════════════
  Id                    UNIQUEIDENTIFIER    PK
  PhoneMasked           NVARCHAR(20)        NOT NULL
  Purpose               TINYINT             (0=Register,1=Login,2=PwdReset,3=MFA)
  AttemptCount          TINYINT             NOT NULL DEFAULT 0
  IsVerified            BIT                 NOT NULL DEFAULT 0
  VerifiedAt            DATETIME2(7)        NULL
  ExpiresAt             DATETIME2(7)        NOT NULL
  CreatedAt             DATETIME2(7)        NOT NULL

═══════════════════════════════════════════════════════════════════
identity.ExternalProviders  (Future social login links)
═══════════════════════════════════════════════════════════════════
  Id                    UNIQUEIDENTIFIER    PK
  UserId                UNIQUEIDENTIFIER    FK → identity.Users(Id)
  Provider              NVARCHAR(50)        (Google, Facebook, Microsoft, Apple)
  ProviderSubjectId     NVARCHAR(256)       NOT NULL (provider's user ID)
  ProviderEmail         NVARCHAR(256)       NULL
  AccessTokenEncrypted  NVARCHAR(MAX)       NULL (KMS-encrypted; future refresh)
  LinkedAt              DATETIME2(7)        NOT NULL
  LastUsedAt            DATETIME2(7)        NULL
  UNIQUE(Provider, ProviderSubjectId)

═══════════════════════════════════════════════════════════════════
identity.AuthAuditLogs  (See §10.1)
═══════════════════════════════════════════════════════════════════
  (structure described above)
```

---

## 12. JWT Token Structure

### 12.1 Token Signing Flow

```
  Farm360.Identity (API)
       │
       │ Assemble payload (header + claims)
       ▼
  AWSSDK.KeyManagementService
       │ KMS Sign API call (payload → KMS)
       │ Private key NEVER leaves KMS
       ▼
  KMS Response: Base64 signature
       │
       │ Assemble JWT: header.payload.signature
       ▼
  Return to client

  VERIFICATION (any party):
  Public key from /.well-known/jwks.json
  → Verify RS256 signature locally (no KMS call)
  → Fast: ~1ms in-process
```

### 12.2 Token Validation Middleware Order

```
[1] Extract Bearer token from Authorization header
[2] Verify RS256 signature (public key in-memory; refreshed from JWKS hourly)
[3] Validate: exp > NOW
[4] Validate: iss == "https://auth.farm360.ai"
[5] Validate: aud == "https://api.farm360.ai"
[6] Validate: jti not in revoked JTI cache (short-lived blocklist, 15min)
[7] Extract: sub (userId), tenant_id, role, farms, tv (tokenVersion)
[8] Cache TV check: Redis.Get("tv:{userId}") → compare with claim tv
    MISMATCH → 401 (token revoked)
    MISS → DB.Users.TokenVersion → compare; cache result 30 seconds
```

---

## 13. Sequence Diagrams

### 13.1 First-Time Registration

```
Mobile App    Farm360 API       Redis          SQL Server     SMS Gateway
    │              │               │                │               │
    ├─register────►│               │                │               │
    │  (phone,name)│               │                │               │
    │              ├─phone exist?──────────────────►│               │
    │              │◄──not found───────────────────┤               │
    │              ├─rate limit?──►│                │               │
    │              │◄──ok──────────┤                │               │
    │              ├─gen OTP       │                │               │
    │              ├─HMAC(OTP)─────►(SET 10min)     │               │
    │              ├─────────────────────────────────────────────── ►│
    │◄─200─────────┤               │      SMS: OTP  │               │
    │              │               │                │               │
    ├─verify───────►│               │                │               │
    │  (phone,otp) │               │                │               │
    │              ├─GET otp hash──►│                │               │
    │              │◄──hash────────┤                │               │
    │              ├─verify HMAC   │                │               │
    │              ├─DEL otp───────►│                │               │
    │              ├─────── BEGIN TX ───────────────►│               │
    │              │               │    INSERT Tenant│               │
    │              │               │    INSERT User  │               │
    │              │               │    INSERT Sub   │               │
    │              ├─────── COMMIT ─────────────────►│               │
    │              ├─issue JWT+RT  │                │               │
    │◄─201─────────┤               │                │               │
    │  (JWT,RT)    │               │                │               │
```

### 13.2 Token Refresh with Rotation

```
Mobile App     Farm360 API        Redis          DB
    │               │                │             │
    ├─refresh───────►│               │             │
    │  (RT=TokenA)   │               │             │
    │               ├─HMAC(TokenA)──►│             │
    │               │◄──MISS─────────┤             │
    │               ├─lookup by hash──────────────►│
    │               │◄──Session (valid)────────────┤
    │               ├─REVOKE old session──────────►│
    │               ├─gen new RT (TokenB)           │
    │               ├─INSERT new session───────────►│
    │               │  (ReplacedBy=OldId)           │
    │               ├─issue new JWT │               │
    │◄─200──────────┤               │               │
    │  (JWT, TokenB) │              │               │
    │               │               │               │
    │   [Theft scenario: attacker uses TokenA again]│
    ├─refresh───────►│               │               │
    │  (RT=TokenA)   │               │               │
    │               ├─lookup──────────────────────►│
    │               │◄──Session (REVOKED!)──────────┤
    │               ├─REVOKE ALL sessions in chain─►│
    │               ├─TokenVersion++─────────────--►│
    │               ├─SMS security alert            │
    │◄─401──────────┤               │               │
```

---

## 14. Security Threat Model

### 14.1 STRIDE Analysis

| Threat | Attack | Control |
|---|---|---|
| **Spoofing** | Forge JWT | RS256 asymmetric — private key in KMS; forgery impossible without key |
| **Spoofing** | Clone refresh token | Stored as HMAC hash; raw token never stored; reuse detection triggers revocation |
| **Tampering** | Modify JWT claims | RS256 signature invalidated; 401 returned |
| **Repudiation** | Deny auth actions | Immutable AuthAuditLogs; IP hash + device fingerprint |
| **Info Disclosure** | Account enumeration | Registration: 200 always; Login: 404/401 (no "wrong password" distinction for OTP) |
| **Info Disclosure** | OTP interception | Short TTL (10 min); single use; SMS encrypted in transit |
| **Denial of Service** | OTP flooding | 5 OTP/phone/hour rate limit; Redis TTL; WAF rules |
| **Elevation of Privilege** | JWT role manipulation | Role embedded in signed JWT; DB role not trusted after JWT |
| **Elevation of Privilege** | TokenVersion bypass | TV check against Redis cache (30s TTL); DB fallback |

---

## 15. Future Integrations

### 15.1 Microsoft Entra ID (Azure AD) — Phase 2 Priority

```
TARGET AUDIENCE: Corporation tier · NGO tier (institutional email domains)
PROTOCOL: OpenID Connect (OIDC) + OAuth 2.0 Authorization Code + PKCE
FLOW:
  [1] User clicks "Sign in with Microsoft" in Angular
  [2] Angular → Microsoft Entra ID authorization endpoint
  [3] User authenticates in Microsoft → returns code
  [4] Angular → Farm360 API /auth/external/microsoft/callback {code, state}
  [5] API → Microsoft token endpoint (exchange code for ID token)
  [6] Validate ID token (iss, aud, nonce)
  [7] Extract email + oid (Microsoft object ID)
  [8] Lookup identity.ExternalProviders WHERE Provider=Microsoft, ProviderSubjectId=oid
      EXISTS  → link to existing Farm360 user → issue tokens
      NOT EXISTS → auto-create user if email domain whitelisted for this tenant
  [9] Return Farm360 JWT + refresh token

TENANT MAPPING:
  Corp tenant registers their Entra ID tenant ID in platform.TenantSettings
  Only users from that Entra tenant can link their account
  Owner must whitelist the AD domain
  
DESIGN HOOK (available now):
  identity.ExternalProviders table already designed
  IExternalAuthProvider interface stub in Infrastructure
```

### 15.2 Google OAuth 2.0 — Phase 2

```
PROTOCOL: OAuth 2.0 Authorization Code + PKCE
FLOW:     Same as Microsoft pattern above (Provider="Google")
CLAIMS:   sub (Google user ID), email, name, picture
MAPPING:  ProviderSubjectId = Google sub claim
USE CASE: Individual farmers with Google accounts (Gmail popular in Bangladesh)
DESIGN:   Same IExternalAuthProvider interface; new GoogleAuthProvider.cs
```

### 15.3 Facebook Login — Phase 3

```
PROTOCOL: Facebook OAuth + Facebook Login JS SDK
FLOW:     Authorization Code from Facebook Graph API
CLAIMS:   id, email (if granted), name
NOTE:     Email permission often denied by users → phone remains primary
USE CASE: Younger farmers in Phase 3 growth markets
```

### 15.4 Mobile Biometric OTP — Phase 2

```
CURRENT:  SMS OTP (works on any phone; no app required)
FUTURE:   In-app biometric verification (Android fingerprint / Face ID)
          + Silent push notification as OTP delivery

FLOW:
  [1] Login initiated on web → push notification to registered mobile device
  [2] User opens Farm360 mobile app → biometric prompt
  [3] Mobile app signs challenge with device-bound key (Keystore/Secure Enclave)
  [4] Signed challenge → Farm360 API → verify device key
  [5] Token issued on web session

DESIGN HOOK:
  identity.UserDevices already tracks device registration
  OtpVerifications.Purpose enum extensible
```

### 15.5 Two-Factor Authentication (TOTP) — Phase 2

```
STANDARD: RFC 6238 (TOTP — Time-based One-Time Password)
APPS:     Google Authenticator · Microsoft Authenticator · Authy
MANDATORY: Owner + FarmManager roles in Corporation tier (Constitution §21.1)

ENROLLMENT FLOW:
  [1] Owner: Settings → Security → Enable 2FA
  [2] Server: Generate TOTP secret (20-byte random)
  [3] Encrypt secret with AWS KMS → store in identity.Users.TotpSecretEncrypted
  [4] Return QR code (otpauth URI) — client renders QR
  [5] User scans with authenticator app
  [6] User submits first TOTP code to confirm enrollment
  [7] Return: 10 recovery codes (BCrypt hashed, stored in DB)
  [8] 2FA active on next login

LOGIN WITH 2FA:
  [1] OTP (SMS) verified → issue SHORT-LIVED token (purpose=MFA-pending, 5 min)
  [2] Client redirects to /verify-2fa
  [3] User submits TOTP code from app
  [4] Server validates: HMAC-SHA1(secret, floor(now/30)) → ±1 window allowed
  [5] Full JWT + refresh token issued

RECOVERY:
  Lost authenticator → use recovery code (10 one-time codes)
  All recovery codes used → contact Platform Admin + SMS OTP verification

DESIGN HOOK:
  TwoFactorEnabled field exists in identity.Users
  TotpSecretEncrypted field exists in identity.Users
  AuthAuditLogs.Purpose enum includes MFA events
```

---

## 16. Risk Analysis

| # | Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| R-01 | JWT private key extracted from KMS | Very Low | Catastrophic | KMS HSM; key rotation; CloudTrail on all KMS API calls |
| R-02 | Refresh token stolen via XSS | Low | High | HttpOnly cookie (device token); refresh token in body (not cookie) — no XSS access; short-lived access token |
| R-03 | OTP brute force | Medium | High | 3-attempt limit; 30-min lockout; rate limit (5/hour); HMAC (no timing attack) |
| R-04 | SMS interception (SIM swap) | Low | High | Account activity notification on all auth events; admin alert on unusual location |
| R-05 | Session fixation | Very Low | Medium | New session created on login (never reuse pre-auth session ID) |
| R-06 | Concurrent session limit bypass | Low | Low | Count check before issuance; idempotent enforcement |
| R-07 | Refresh token family poisoning | Very Low | High | Reuse detection revokes entire family; immediate TokenVersion increment |
| R-08 | Privilege escalation via role claim | Very Low | Critical | Role embedded in RS256-signed JWT; server never reads role from user-controlled input |
| R-09 | TOTP secret extraction from DB | Very Low | High | KMS envelope encryption; secret never in plaintext; migration to HSM if needed |
| R-10 | Bot registration via OTP farming | Medium | Medium | Rate limiting; phone number risk scoring (Phase 2); WAF bot detection |
| R-11 | Audit log tampering | Very Low | High | INSERT-only DB permission on AuthAuditLogs; S3 archive backup daily |
| R-12 | Token expiry clock skew | Low | Low | 30-second leeway in JWT validation; server-side NTP sync |

---

## 17. Testing Strategy

### 17.1 Authentication Unit Tests

```
OtpService:
  □ GenerateOtp_Returns6DigitNumeric
  □ StoreOtp_HashesCorrectly_NeverStoresPlaintext
  □ VerifyOtp_ValidCode_ReturnsTrue
  □ VerifyOtp_InvalidCode_ReturnsFalse
  □ VerifyOtp_AfterExpiry_ReturnsFalse
  □ VerifyOtp_Attempt4_ThrowsLockoutException

JwtTokenService:
  □ IssueToken_ContainsAllRequiredClaims
  □ IssueToken_ExpiresIn15Minutes
  □ IssueToken_EmbedsTenantId
  □ IssueToken_EmbedsTokenVersion
  □ ValidateToken_ValidToken_ReturnsClaimsPrincipal
  □ ValidateToken_ExpiredToken_Throws
  □ ValidateToken_WrongAudience_Throws
  □ ValidateToken_TamperedPayload_Throws (RS256 validation)

RefreshTokenService:
  □ Rotate_ValidToken_IssuesNewToken_RevokesOld
  □ Rotate_ExpiredToken_Throws
  □ Rotate_RevokedToken_ThrowsAndRevokesFamily
  □ Rotate_DeviceFingerprintMismatch_LogsWarning_Proceeds
```

### 17.2 Authentication Integration Tests

```
Registration:
  □ RegisterUser_NewPhone_Returns201_WithTokens
  □ RegisterUser_ExistingPhone_Returns409
  □ RegisterUser_InvalidOtp_Returns422
  □ RegisterUser_ExpiredOtp_Returns422
  □ RegisterUser_3FailedAttempts_LocksPhone

Login:
  □ Login_ValidCredentials_Returns200_WithTokens
  □ Login_LockedAccount_Returns423_WithRetryAfter
  □ Login_SuspendedTenant_Returns402
  □ Login_TrustedDevice_SkipsOtp_Returns200
  □ Login_UnknownDevice_RequiresOtp

Token:
  □ Refresh_ValidToken_Returns200_NewTokens
  □ Refresh_ExpiredToken_Returns401
  □ Refresh_RevokedToken_RevokesFamily_Returns401
  □ Refresh_ReuseDetected_IncrementsTokenVersion_SendsAlert

Authorization:
  □ Worker_AccessesFinanceEndpoint_Returns403
  □ Owner_AccessesAllEndpoints_Returns200
  □ FarmManager_AccessesOtherFarm_Returns404 (ABAC)
  □ Viewer_PostsAnimal_Returns403

Multi-Tenant:
  □ TenantA_User_CannotAccessTenantB_Data
  □ TenantA_Token_TenantB_Tenant_Returns402

Lockout:
  □ 5ConsecutiveFailures_LocksAccount_Returns423
  □ AdminUnlock_PermitsNextLogin
  □ LockoutExpiry_PermitsNextLogin_Automatically

Session:
  □ LogoutSingleSession_RevokesRefreshToken_Succeeds
  □ LogoutAll_RevokesAllSessions_IncrementsTokenVersion
  □ SessionList_ShowsOnlyActiveSessions
  □ RevokeSpecificSession_OtherSessionsUnaffected
```

### 17.3 Security Penetration Test Checklist

```
□ JWT: Forged token with HS256 (algorithm confusion attack)
□ JWT: None algorithm attack
□ JWT: Modified tenant_id claim (signature must fail)
□ OTP: Sequential OTP guessing (rate limit must trigger)
□ OTP: Parallel OTP submission (race condition — Redis atomic increment)
□ Refresh: Token reuse after rotation (family revocation must trigger)
□ Sessions: Exceed concurrent session limit
□ IDOR: Access another user's sessions list
□ IDOR: Revoke another user's session
□ Timing: OTP comparison timing attack (HMAC constant-time compare)
□ Cookie: Device token usable via JavaScript (HttpOnly must block)
□ Cookie: Device token sent to wrong path (SameSite=Strict)
□ CSRF: Request without Origin header (SameSite protects)
□ Logout: Access token valid after logout (15min window — documented)
□ Privilege: Viewer role escalation to Owner
□ Account: Registration with invalid +880 number formats
□ Reset: Password reset without OTP verification
□ Recovery: TOTP recovery code reuse
```

---

## Architecture Finalization Checklist

Before implementation begins:

- [ ] JWT access token 15-minute expiry approved (Security + Product)
- [ ] Refresh token 30-day rolling window approved
- [ ] 3-attempt OTP lockout policy confirmed (UX impact considered)
- [ ] Phone-first (no email required) registration confirmed by Product
- [ ] Remember Device 90-day cookie policy approved by Security
- [ ] Concurrent session limits per tier approved by Product
- [ ] Progressive lockout doubling policy approved by Security
- [ ] Audit log 7-year retention approved by Legal/Compliance
- [ ] IP hashing algorithm (HMAC-SHA256 with server secret) approved by Privacy/DPO
- [ ] Entra ID integration design approved for Corporation tier (Phase 2 confirmed)
- [ ] TOTP mandatory for Owner+FarmManager in Corp tier approved

---

*This document is the authoritative reference for all authentication and authorization design on Farm360 AI.*  
*Governed by: F360-CONST-2026-001 — Project Constitution.*  
*© 2026 Farm360 AI Engineering Organization. All Rights Reserved.*
