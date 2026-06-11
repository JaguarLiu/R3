# R3 Web Authentication & Authorization (JWT) — Design

**Date:** 2026-06-10
**Status:** Approved
**Scope:** TODO.md Priority 0 — Web 端身份驗證 + `SplitExpense` audit columns (`CreatedBy` + `SourceChannel`).

## Problem

The web REST API (`/api/*`) has no authentication. Anyone who knows a `tripId` can read and
write any trip's data. Before exposing the app publicly we must require login and enforce
per-trip access control. Separately, expenses don't record who created them or from which
channel, which the audit/debug story needs.

Additionally, `appsettings.json` currently contains **live committed secrets** (Postgres
password, LINE channel secret + access token, Gemini API key). This is the most acute current
exposure and is remediated as part of this work.

## Decisions (from brainstorming)

1. **Access model:** Owner + members. A `Trip` has an owner (`OwnerUserId`) and optional
   additional members (`TripMember`). Authorization = owner OR member.
2. **LINE relationship:** Keep the two worlds separate. The LINE messaging bot/webhook keeps
   working exactly as today (LINE-signature authenticated, scoped by `LineGroupId`, no owner
   required). **LINE Login** is only a second way to authenticate a *web* account. LINE-group
   trips are not surfaced in the web UI. (Auto-bridging by group membership is infeasible — LINE
   provides no "list my groups" API. Claiming trips is a possible future iteration.)
3. **Tokens:** Short-lived access JWT (~15 min) held in browser memory + long-lived refresh
   token in an httpOnly, Secure, SameSite=Lax cookie, with server-side rotation and revocation.

## Non-goals (explicitly deferred)

- Bridging LINE-group trips into the web ownership model (claim/import flow).
- Linking `Participant` (a name in the split math) to a `User` account.
- Full email-invite flow for non-registered users (we only add *existing* users by email).
- Rewriting git history to purge already-committed secrets (flagged; done only on request).
- Endpoint/integration test suite (unit tests for security-critical logic only).

## Data model

### New entities

```
User
  Id            long  (PK)
  Email         string?   -- unique when present; LINE-only users may have none
  PasswordHash  string?   -- null for LINE-only accounts (BCrypt)
  DisplayName   string
  LineUserId    string?   -- unique when present; from LINE Login
  CreatedAt     DateTime
```
One account may have email+password, LINE, or both. A LINE login that matches an existing
`Email` is out of scope to auto-link; matching is by `LineUserId`.

```
RefreshToken
  Id         long  (PK)
  UserId     long  (FK -> User, cascade)
  TokenHash  string     -- random opaque token, hashed at rest (SHA-256)
  ExpiresAt  DateTime
  RevokedAt  DateTime?
  CreatedAt  DateTime
  index (TokenHash)
```

```
TripMember            -- non-owner members; owner is tracked on Trip
  Id         long  (PK)
  TripId     long  (FK -> Trip, cascade)
  UserId     long  (FK -> User, cascade)
  CreatedAt  DateTime
  unique (TripId, UserId)
```

### Changes to existing tables

- `Trip`: add `OwnerUserId long?` (FK -> User). Null for LINE-created trips. Index on `OwnerUserId`.
- `SplitExpense`: add
  - `CreatedByUserId long?` — web creator's user id; null for LINE.
  - `CreatedByName string?` — display name of the creator (web user name or LINE sender name).
  - `SourceChannel string` — `"web"` or `"line"`. Default existing rows to `"line"` in the migration.

`Participant` is unchanged and deliberately distinct from `User`/`TripMember`.

## Authentication

New `Endpoints/AuthEndpoints.cs`, mapped under `/api/auth`, rate-limited by a new `auth`
policy (10 req/min per IP, mirroring the existing `ai` fixed-window pattern in `Program.cs`).

### Email + password
- `POST /api/auth/register` — body `{ email, password, displayName }`. Validate email format and
  password length (>= 8). Reject duplicate email. Hash with BCrypt. Create `User`. Issue tokens.
- `POST /api/auth/login` — body `{ email, password }`. Verify hash. Generic failure message
  (don't reveal which field failed). Issue tokens.

### LINE Login (backend-driven OAuth 2.0 — channel secret stays server-side)
- `GET /api/auth/line/start` — generate `state` (CSRF, stored in a short-lived signed/httpOnly
  cookie), redirect to LINE's `authorize` endpoint with scope `profile openid`.
- `GET /api/auth/line/callback?code&state` — validate `state` against the cookie, exchange `code`
  for a LINE access token, fetch the LINE profile (`userId`, `displayName`), find-or-create a
  `User` by `LineUserId`, issue tokens (set refresh cookie), then HTTP-redirect to the SPA root.
- **Prerequisite:** a LINE **Login** channel, separate from the Messaging API bot.
  Config: `LineLogin:ChannelId`, `LineLogin:ChannelSecret`, `LineLogin:CallbackUrl`.

### Token issuance (shared step)
- **Access JWT** — HS256, signed with `Jwt:SignKey`. Claims: `sub` = userId, `name` = displayName,
  `iss` = `Jwt:Issuer`, `aud` = `Jwt:Audience`, `exp` ~15 min. Returned in the JSON response body.
  The SPA keeps it in a module-level variable (memory only).
- **Refresh token** — cryptographically random opaque value. Stored **hashed** (SHA-256) in
  `RefreshToken`. Delivered in an httpOnly, Secure, SameSite=Lax cookie scoped to path `/api/auth`,
  lifetime `Jwt:RefreshTokenDays` (~14d).

### Session lifecycle
- `POST /api/auth/refresh` — read refresh cookie, look up by hash, ensure not expired/revoked,
  **rotate**: mark old row revoked, issue a new refresh token + new access JWT. A presented token
  that is already revoked is rejected (reuse detection).
- `POST /api/auth/logout` — revoke the current refresh token, clear the cookie.
- `GET /api/auth/me` — return `{ id, email, displayName, lineLinked }` for the current user.

### Libraries
- `Microsoft.AspNetCore.Authentication.JwtBearer` — token validation middleware.
- `BCrypt.Net-Next` — password hashing.

## Authorization

### Wiring (`Program.cs`)
- `AddAuthentication(JwtBearerDefaults).AddJwtBearer(...)` validating issuer/audience/lifetime/key.
- `AddAuthorization()`.
- `app.UseAuthentication()` then `app.UseAuthorization()`, placed after `UseRateLimiter`.
- `/webhook` remains anonymous (authenticated by LINE signature, not JWT).
- `/api/health` remains anonymous.

### Helper: `TripAccess`
- `ClaimsPrincipal.CurrentUserId()` extension → parses `sub` claim to `long`.
- `RequireAccess(AppDbContext db, long tripId, long userId, bool ownerOnly = false)` →
  returns the `Trip` if the user is the owner (or a member, when `!ownerOnly`), else `null`.
  Handlers translate `null` into **404** (not 403) to avoid leaking trip existence.

### Endpoint matrix (all require an authenticated user)
| Endpoint | Rule |
|---|---|
| `GET /api/trips` | Return only trips where caller is owner OR member |
| `POST /api/trips` | Allowed; sets `OwnerUserId = caller` |
| `GET /api/trips/{id}` | Owner or member; else 404 |
| `PUT /api/trips/{id}` | **Owner only**; else 404 |
| `DELETE /api/trips/{id}` | **Owner only**; else 404 |
| `POST/PUT/DELETE /api/trips/{tripId}/expenses[/{id}]` | Owner or member; stamp `CreatedByUserId`, `CreatedByName`, `SourceChannel="web"` on create |
| `POST /api/ai/analyze/{tripId}` | Owner or member |
| `POST /api/ai/parse/{tripId}` | Owner or member; stamps web audit fields on created rows |
| `POST /api/trips/{id}/members` `{ email }` | **Owner only**; adds an existing user by email; unknown email → 404 |
| `DELETE /api/trips/{id}/members/{userId}` | **Owner only** |

`GET /api/trips/{id}` additionally returns the member list.

### Webhook stamping
`HandleRecord` (and the regex fallback path) set `SourceChannel = "line"`,
`CreatedByName = <sender display name>`, `CreatedByUserId = null` on every `SplitExpense` written.

## Frontend (`Client/`)

- **Login / register screen**: email+password form plus a "Sign in with LINE" button that does
  `window.location = '/api/auth/line/start'`.
- **`api.js`**: hold the access token in a module-level variable; attach `Authorization: Bearer`.
  Always send `credentials: 'include'` (so the refresh cookie rides along). On a `401`, call
  `/api/auth/refresh` exactly once and retry the original request; if refresh fails, clear state
  and show the login screen.
- **Bootstrap on load** (`app.jsx`): the access token is memory-only and lost on reload, so on
  startup call `/api/auth/refresh` to restore the session from the cookie before rendering the app.
- **Trip selection**: keep `localStorage 'r3.tripId'` as "last opened," validated against
  `GET /api/trips`; if absent from the caller's list, fall back to the returned list. (A full trip
  picker remains the existing P2 TODO.)

## Configuration

All secrets via user-secrets/env — never `appsettings.json`.
```
Jwt:SignKey             >= 32 bytes, secret
Jwt:Issuer              e.g. "r3"
Jwt:Audience            e.g. "r3-web"
Jwt:AccessTokenMinutes  15
Jwt:RefreshTokenDays    14
LineLogin:ChannelId
LineLogin:ChannelSecret
LineLogin:CallbackUrl   e.g. https://<host>/api/auth/line/callback
```

### Secret remediation (part of this work)
- Move the existing Postgres / LINE / Gemini values out of `appsettings.json` into user-secrets;
  leave empty placeholders in the file.
- **Rotate all of them** — they exist in git history. (History rewrite is out of scope unless
  requested; flagged to the user.)
- Secure cookies require HTTPS in production.

## Migration

Single migration `AddAuthAndAuditColumns`:
- Create `Users`, `RefreshTokens`, `TripMembers`.
- Add `Trip.OwnerUserId` (nullable, indexed).
- Add `SplitExpense.CreatedByUserId` (nullable), `CreatedByName` (nullable), `SourceChannel`
  (non-null, default `"line"` for existing rows).

Existing LINE trips correctly get `OwnerUserId = null`. Any pre-existing **web** trips become
orphaned (no owner) — acceptable in dev; a manual backfill is noted if real data exists.

## Testing

New xUnit project covering the highest-risk pure logic (TDD):
- JWT issue → validate roundtrip (valid, expired, wrong key/issuer/audience).
- Password hash → verify (correct + wrong password).
- Refresh-token rotation: rotate issues new token and revokes old; reuse of a revoked token is rejected.
- `TripAccess.RequireAccess`: owner allowed; member allowed; stranger denied; `ownerOnly` excludes members.

Endpoint/integration tests are a stretch goal, not P0.

## Build order

1. Data model + migration.
2. JWT / password / refresh-token services + unit tests.
3. Auth endpoints (`/api/auth/*`) + `auth` rate-limit policy.
4. Authorization on trip/expense/ai endpoints + member management + webhook stamping.
5. Frontend: login/register screen, `api.js` token handling, startup refresh bootstrap.
6. Secret remediation (move to user-secrets, placeholders, rotation guidance).
