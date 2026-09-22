# Feature Specification: Login and Authentication — MVP

**Feature Branch**: `004-login-authentication`
**Created**: 2026-07-28
**Last Updated**: 2026-07-28
**Status**: Draft — Ready for Implementation
**Constitution Reference**: NovaLeave — Constitution v4.0.0
**Use Cases Reference**: `casos-de-uso-spec_004.md`
**Screen Construction Reference**: `spec_003-screen-construction-guide.md`

---

## Overview

This specification covers the authentication and session management workflow for NovaLeave MVP: a User or Approver authenticates with their pre-provisioned credentials, receives a secure session cookie, and can access role-appropriate system features. It resolves the functional behavior required by Constitution §7.1 (Authentication and Authorization) and §4 (MVP Actors and Authorization) into testable, implementation-ready requirements.

The login system uses **ASP.NET Core Identity** with secure cookie authentication as mandated by Constitution §3.2 and §7.1. There is no custom session or token mechanism. The system supports two application roles — `User` and `Approver` — and a person may hold both roles.

**Out of scope for this spec**: account creation/self-registration, password reset, account recovery, multi-factor authentication, social login providers, API authentication (JWT/Bearer), or any authentication mechanism other than the default ASP.NET Core Identity cookie authentication. These require their own specifications before implementation.

---

## Key PO Decisions (Documented)

The following decisions are incorporated into this specification from the Constitution and Product Owner decisions:

- **No self-registration**: Accounts are pre-provisioned by an external process (HR/IT). There is no auto-registration in the MVP.
- **Account status**: Accounts have an `Active` or `Inactive` status. Only `Active` accounts may authenticate.
- **Role-based access**: Users hold the `User` role, `Approver` role, or both. Authentication does not distinguish between roles until post-authentication redirection.
- **Cookie-based authentication**: Sessions are managed via ASP.NET Core Identity secure cookies (not JWT or custom tokens).
- **Existence hiding**: Authentication failures must not reveal whether an account exists, is inactive, or is locked out. All failures return the same generic error message.
- **Rate limiting**: Login endpoint is protected by rate limiting independent of Identity's lockout mechanism.
- **Audit logging**: All authentication attempts (successful and failed) are logged without exposing passwords or sensitive PII.

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - User Authenticates with Valid Credentials (Priority: P1)

As a **User or Approver**, I want to log in with my email and password, so that I can access the system's features according to my role.

**Why this priority**: Authentication is the entry point to the entire system. Without it, no other feature is accessible.

**Independent Test**: Navigate to `/Account/Login`, enter valid credentials for an `Active` account, and verify the user is redirected to the appropriate dashboard with an active session.

**Acceptance Scenarios**:

1. **Given** an unauthenticated visitor with an `Active` account, **When** they submit valid email and password, **Then** the system shall authenticate the user, emit a secure session cookie, redirect to the role-appropriate dashboard (User Dashboard by default, or Approver Dashboard if the user holds only the `Approver` role), and record a successful login audit event.
2. **Given** an unauthenticated visitor, **When** they submit valid credentials but the account status is `Inactive`, **Then** the system shall deny access with the same generic error message as for invalid credentials, not reveal the account status, and record a failed login audit event with internal reason "InactiveAccount".
3. **Given** an unauthenticated visitor who was redirected to Login from a protected page (e.g., `/Employee/Requests/Create`), **When** they authenticate successfully, **Then** the system shall redirect to the originally requested page if it remains valid and authorized; otherwise redirect to the default dashboard.
4. **Given** an already authenticated user, **When** they navigate to `/Account/Login`, **Then** the system shall detect the active session and redirect to the role-appropriate dashboard without re-prompting for credentials.
5. **Given** a user with both `User` and `Approver` roles, **When** they authenticate successfully, **Then** the system shall default to the User Dashboard and the sidebar shall show both navigation sections with the Role Switcher available.

---

### User Story 2 - System Denies Access for Invalid Credentials (Priority: P1)

As an **unauthenticated visitor**, when I attempt to log in with invalid credentials, I want to receive a generic error message that does not reveal whether my email exists or my account status, so that the system does not leak sensitive information to attackers.

**Why this priority**: Security is a constitutional requirement (OWASP A01, A06). Generic error messages prevent information disclosure that could be used for account enumeration attacks.

**Independent Test**: Submit login with an unknown email, an incorrect password for a known email, and an inactive account. Verify the same generic error message is displayed in all three cases.

**Acceptance Scenarios**:

1. **Given** an unauthenticated visitor, **When** they submit an email that does not exist in the system, **Then** the system shall display the generic error message "Invalid email or password." and shall not indicate whether the email exists.
2. **Given** an unauthenticated visitor, **When** they submit a valid email with an incorrect password, **Then** the system shall display the same generic error message, increment the `AccessFailedCount` for the account, and record a failed login audit event with internal reason "InvalidPassword".
3. **Given** an account that has reached `MAX_FAILED_ACCESS_ATTEMPTS`, **When** a login attempt is made (even with correct password), **Then** the system shall deny access with the same generic error message, not reveal that the account is locked, and record a failed login audit event with internal reason "LockedOut".
4. **Given** a login attempt that exceeds the rate limit configured for the login endpoint, **When** the threshold is exceeded, **Then** the system shall reject the request with HTTP 429 before verifying credentials, and shall not increment `AccessFailedCount` for any account.

---

### User Story 3 - User Logs Out (Priority: P1)

As an **authenticated user**, I want to securely log out of the system, so that my session is invalidated and no further requests can be made with my credentials.

**Why this priority**: Logout completes the authentication lifecycle and is a security baseline requirement (Constitution §7.1).

**Independent Test**: Log in, click the "Sign Out" button, and verify the session cookie is invalidated and the user is redirected to the Login page.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they click "Sign Out" in the sidebar footer, **Then** the system shall send a POST request to `/Account/Logout` with an anti-forgery token, invalidate the session cookie, record a logout audit event, and redirect to the Login page.
2. **Given** an authenticated user, **When** a logout POST request is sent without a valid anti-forgery token, **Then** the system shall reject the request with HTTP 400/403, and the session shall remain active.
3. **Given** a user who has logged out, **When** they attempt to access a protected page with the old cookie, **Then** the system shall deny access and redirect to Login.

---

### User Story 4 - Session Expiration and Revalidation (Priority: P2)

As an **authenticated user**, when my session expires due to inactivity or exceeds its absolute lifetime, I want to be redirected to the Login page with my original destination preserved, so that I can re-authenticate and continue my work.

**Why this priority**: Session expiration is a standard security control; the system can function without it but would violate Constitution §7.1 requirements.

**Independent Test**: Authenticate, wait for inactivity timeout (20 minutes), then attempt to access a protected page. Verify the user is redirected to Login with the original URL preserved for post-login redirection.

**Acceptance Scenarios**:

1. **Given** an authenticated user with an inactive session exceeding the configured idle timeout (20 minutes), **When** they attempt to access a protected page, **Then** the system shall redirect to Login with the original URL preserved.
2. **Given** an authenticated user with a session cookie that has exceeded its absolute lifetime (8–12 hours), **When** they attempt to access a protected page, **Then** the system shall redirect to Login.
3. **Given** an authenticated user whose account status was changed to `Inactive` during an active session, **When** they attempt a state-changing operation, **Then** the system shall revalidate the account status server-side, deny access, redirect to Login, and record an authorization failure audit event.
4. **Given** an authenticated user whose role was changed during an active session, **When** they attempt an operation requiring the removed role, **Then** the system shall revalidate the role server-side, deny access, and the security stamp update shall invalidate the session.

---

## Requirements *(mandatory)*

### Functional Requirements

#### Authentication

- **FR-001**: The system **shall** provide a Login page at `/Account/Login` with a form containing Email and Password fields, a "Sign In" button, and a generic error message display area.
- **FR-002**: The system **shall** authenticate users exclusively via ASP.NET Core Identity with secure cookie authentication (no custom session or token implementation).
- **FR-003**: The system **shall** accept login credentials only for accounts with status `Active`. Accounts with status `Inactive` **shall** be denied access with the same generic error message as invalid credentials.
- **FR-004**: The system **shall** use a dedicated `LoginViewModel` for login requests (containing only Email, Password, and anti-forgery token) and **shall not** bind directly to Identity or Domain entities.
- **FR-005**: The system **shall** validate anti-forgery tokens for all POST requests to `/Account/Login` and `/Account/Logout`.
- **FR-006**: The system **shall** apply rate limiting to the login endpoint (`/Account/Login`) independent of Identity's lockout mechanism (Constitution §7.2).
- **FR-007**: The system **shall** display the Login page with an independent layout (no sidebar) as defined in `spec_003-screen-construction-guide.md` §3.1.

#### Session Management

- **FR-008**: The system **shall** emit a session cookie with the following security settings:
  - `HttpOnly`: true (not accessible to client-side scripts)
  - `Secure`: true in production (HTTPS only)
  - `SameSite`: `Lax` or `Strict`
  - Sliding expiration with absolute lifetime of 8–12 hours
  - Idle timeout of 20 minutes
- **FR-009**: The system **shall** invalidate the session cookie upon logout (`/Account/Logout` POST).
- **FR-010**: The system **shall** update the Identity security stamp when account roles or status change, invalidating all existing session cookies (Constitution §7.1).
- **FR-011**: The system **shall** revalidate identity, role, and `Active` status server-side before any state-changing operation, not relying solely on cached claims in the cookie (Constitution §5 invariant 12).

#### Redirection and Navigation

- **FR-012**: After successful authentication, the system **shall** redirect to the originally requested page if the visitor arrived at Login via a redirect from a protected page and the page remains valid and authorized.
- **FR-013**: If no original page is preserved, or the preserved page is no longer valid/authorized, the system **shall** redirect to the default dashboard: User Dashboard if the user holds the `User` role (including users with both roles), or Approver Dashboard if the user holds only the `Approver` role.
- **FR-014**: An already authenticated user navigating to `/Account/Login` **shall** be redirected to the role-appropriate dashboard without re-prompting for credentials.

#### Lockout and Rate Limiting

- **FR-015**: The system **shall** apply Identity's built-in lockout mechanism: after `MAX_FAILED_ACCESS_ATTEMPTS` consecutive failed login attempts, the account **shall** be locked for `LOCKOUT_DURATION_MINUTES`.
- **FR-016**: The system **shall** display the same generic error message for all authentication failures (unknown email, incorrect password, inactive account, locked account) without revealing the specific reason.
- **FR-017**: The system **shall** reset `AccessFailedCount` to zero upon successful authentication.

#### Audit and Logging

- **FR-018**: The system **shall** record an audit entry for every authentication attempt (successful and failed) containing:
  - `timestamp_utc`
  - `actor_id` (if resolvable; otherwise "unknown")
  - `action`: `LoginSuccess`, `LoginFailure`, or `Logout`
  - `result` (success/failure)
  - Internal reason category for failures (not exposed to the user)
  - `correlation_id`
  - `request_id`
- **FR-019**: Audit logs **shall not** contain passwords or sensitive PII (Constitution §7.3, §8).

---

### Non-Functional Requirements

#### Performance

- **NFR-001**: The Login page **shall** load with p95 < 300 ms server-side response time under expected load.
- **NFR-002**: Authentication (POST to `/Account/Login`) **shall** complete with p95 < 500 ms under expected load.

#### Security

- **NFR-003**: All authentication cookies **shall** be `HttpOnly` and `Secure` in production (Constitution §7.1).
- **NFR-004**: The system **shall** enforce TLS 1.2+ and HSTS in production.
- **NFR-005**: Login attempts **shall** be rate-limited to prevent brute-force attacks (Constitution §7.2).
- **NFR-006**: The login endpoint **shall** return HTTP 429 (Too Many Requests) when the rate limit is exceeded.

#### Accessibility

- **NFR-007**: The Login page **shall** meet WCAG 2.1 AA requirements: proper labels, keyboard navigation, visible focus, and sufficient contrast.
- **NFR-008**: Form fields **shall** have associated accessible labels and validation error messages with `role="alert"`.

#### Responsive Design

- **NFR-009**: The Login page **shall** be responsive and maintain a single-column centered layout on all breakpoints (mobile, tablet, desktop) as defined in `spec_003-screen-construction-guide.md` §3.1.

---

## Key Entities

- **IdentityUser**: ASP.NET Core Identity user account. Attributes: `Id`, `Email`, `PasswordHash`, `AccessFailedCount`, `LockoutEnd`, `LockoutEnabled`, `SecurityStamp`.
- **Account Status**: Tracks `Active`/`Inactive` status of the account. This is stored as a custom attribute on the IdentityUser or linked entity. Only `Active` accounts may authenticate.
- **User/Approver Role**: Role claims assigned to the IdentityUser. A user may hold `User`, `Approver`, or both.
- **AuditRecord**: Immutable record of authentication events. Attributes: `Id`, `Timestamp` (UTC), `ActorId`, `Action` (`LoginSuccess`, `LoginFailure`, `Logout`), `Result`, `Reason` (internal), `CorrelationId`, `RequestId`.

---

## Documented Design Decisions

The following decisions are purely technical and do not affect business policy. They are documented here to ensure implementation consistency.

- **D-001 — ASP.NET Core Identity with Cookies**: The system uses ASP.NET Core Identity with secure cookie authentication as mandated by Constitution §3.2 and §7.1. No JWT, Bearer token, or custom session mechanism is used in the MVP. This is the constitutional default.

- **D-002 — Anti-Forgery Token Validation**: All unsafe browser requests (`POST`, `PUT`, `PATCH`, `DELETE`) must validate anti-forgery tokens. The project enforces this globally with `AutoValidateAntiforgeryToken` (Constitution §7.2).

- **D-003 — Generic Error Messages**: The system returns the same error message ("Invalid email or password.") for all authentication failures: unknown email, incorrect password, inactive account, and locked account. This prevents account enumeration attacks (OWASP A01).

- **D-004 — Rate Limiting**: The login endpoint is rate-limited independently of Identity's lockout mechanism. This provides defense-in-depth against brute-force attacks (Constitution §7.2).

- **D-005 — Security Stamp Invalidation**: When account roles or status change, the Identity security stamp is updated, invalidating all existing session cookies. Users must re-authenticate to obtain updated claims (Constitution §7.1).

- **D-006 — Dedicated LoginViewModel**: The system uses a dedicated `LoginViewModel` (Email, Password, anti-forgery token) for login requests. It does not bind directly to IdentityUser or Domain entities, preventing over-posting attacks (Constitution §7.2, SR-002 from `spec_001`).

- **D-007 — Role-Based Default Redirection**: Users with the `User` role (including users with both roles) default to the User Dashboard. Users with only the `Approver` role default to the Approver Dashboard. This is consistent with Constitution §4.4 and `spec_003-screen-construction-guide.md`.

- **D-008 — Auditoría de Intentos de Login**: Cada intento de autenticación (exitoso o fallido) se registra en el registro de auditoría. Los registros de fallos incluyen una categoría de razón interna (por ejemplo, "InvalidPassword", "InactiveAccount", "LockedOut") que no se expone al usuario. Los registros no contienen la contraseña enviada.

- **D-009 — Post/Redirect/Get for Logout**: Logout follows Post/Redirect/Get pattern: the user submits a POST to `/Account/Logout`, the session is invalidated, and the user is redirected (GET) to `/Account/Login`. This prevents accidental duplicate submissions (Constitution §7.2, `spec_003-screen-construction-guide.md`).

- **D-010 — Expiración de Sesión**: La cookie de sesión utiliza expiración deslizante (sliding expiration) con una vida útil absoluta de 8 a 12 horas y un tiempo de inactividad máximo de 20 minutos. La expiración se evalúa en cada solicitud (Constitution §7.1, CU-015).

---

## Security & Privacy Requirements

The following requirements are derived from the NovaLeave Constitution (v4.0.0) and industry best practices. They are mandatory for the MVP and must be implemented alongside functional requirements. Each security requirement is testable and enforceable through automated tests.

### SR-001 — Anti-CSRF Protection

Every HTTP request that changes system state (`POST`, `PUT`, `PATCH`, `DELETE`) **must** validate an anti-forgery token (anti-CSRF) generated by the server and rendered in the form. Requests lacking a valid token **must** be rejected with an HTTP 400 or 403 error, and the action **must not** be executed.

**Testing**: Unit/integration tests must verify that requests without a valid token are rejected, and that valid tokens are accepted.

**Coverage**: This applies to `/Account/Login` (POST) and `/Account/Logout` (POST) as specified in CU-012, CU-013, CU-014.

---

### SR-002 — Dedicated Input Models

Each state-changing operation **must** receive user input through a dedicated ViewModel or command object that contains **only** the fields required for that specific operation. The system **must not** directly bind HTTP request data to domain entities, to prevent over-posting attacks.

**Testing**: Integration tests must verify that extra fields sent in the login request are ignored by the model binder or rejected.

**Coverage**: Login uses a dedicated `LoginViewModel` (Email, Password, anti-forgery token). Logout requires anti-forgery token only.

---

### SR-003 — Existence Hiding for Unauthorized Access

When an authenticated user requests a specific resource and they are **not** authorized to view it, the system **must** respond in a way that makes "resource does not exist" and "resource exists but you are not authorized" indistinguishable.

**Implementation**: Return HTTP 404 for both cases. Do **not** return HTTP 403 with a distinct message.

**Testing**: Test suites must verify that for a valid but unauthorized resource ID, the response is identical to that for a non-existent ID.

**Coverage**: This applies to direct URL access to protected resources, including role-appropriate dashboard routes (e.g., an Employee accessing `/Approver/Dashboard` should receive 404).

---

### SR-004 — Session Revalidation at Execution Time

Authorization and identity **must** be revalidated at the **exact moment** a state-changing operation is processed, not at the moment the page was loaded. If the session is invalid, expired, or the user no longer holds the required role/relationship at that time, the operation **must** be rejected (fail-closed) without applying any partial changes.

**Implementation**: Every handler or controller action must validate the current session state, including authentication status, role, and active status, before executing business logic. This cannot rely on cached values from earlier in the same request.

**Testing**: Tests must simulate expired sessions, role removals, and status changes occurring after page load, verifying that the operation is rejected.

**Coverage**: This applies to all protected actions as specified in CU-015 and Constitution §5 invariant 12.

---

### SR-005 — Secure Cookie Configuration

Authentication cookies (ASP.NET Core Identity) **must** be configured with the following settings in production:
- `HttpOnly`: true
- `Secure`: true
- `SameSite`: `Strict` or `Lax`
- Sliding expiration with absolute lifetime of 8–12 hours
- Idle timeout of 20 minutes
- Revocation behavior: when the user logs out or their role changes, the security stamp **must** be updated, invalidating all existing cookies

**Testing**: Test suites must verify that cookies are not readable by client‑side scripts, are only sent over HTTPS, and that role changes/logout invalidate the session.

**Coverage**: This applies to all authenticated sessions as specified in CU-012 (postconditions) and Constitution §7.1.

---

### SR-006 — Rate Limiting

Sensitive endpoints (login, password reset, account recovery) **must** apply rate limiting proportional to risk (Constitution §7.2). The login endpoint **must** be rate-limited independently of Identity's lockout mechanism.

**Testing**: Tests must verify that exceeding the rate limit threshold returns HTTP 429 and does not increment `AccessFailedCount`.

**Coverage**: This applies to `/Account/Login` (POST) as specified in CU-013, FE-002, and Constitution §7.2.

---

### SR-007 — Generic Error Messages

Authentication failures **must** return the same generic error message for all failure categories: unknown email, incorrect password, inactive account, locked account. No internal state (existence of account, category of failure, lockout status) may be revealed to the visitor beyond the generic message.

**Testing**: Tests must verify that the same error message is returned for all failure categories, and that no status-specific information is leaked in the response.

**Coverage**: This applies to CU-013 (all failure scenarios) and Constitution §7.1.

---

### SR-008 — Audit Logging for All Security-Relevant Events

The system **must** log the following events in a tamper‑evident audit trail (immutable storage):
- Successful and failed authentication attempts (with internal reason category for failures)
- Logout events
- Failed authorization attempts (e.g., IDOR attempts, cross‑role access attempts)
- Session invalidation events

Logs **must not** contain sensitive data such as passwords, tokens, or the full `reason` field (unless explicitly authorized by a specific use case). The audit trail **must** be protected against unauthorized modification or deletion.

**Testing**: Integration tests must verify that each security event generates an audit record with all required fields populated.

**Coverage**: This applies to CU-012 (POST-002), CU-013 (POST-002), CU-014 (POST-002), and Constitution §8.

---

### SR-009 — Data Minimization and Privacy Protection

The system **must** collect and store only the minimum data necessary for authentication and session management. Specifically:
- The login form collects only Email and Password (no additional PII).
- Audit logs must not contain passwords or tokens.
- Session cookies contain only the authentication identifier, not PII.

**Testing**: Tests must verify that no sensitive data is stored in logs, cookies, or visible in the UI beyond what is necessary for authentication.

**Coverage**: This applies to Constitution §7.3 and §8.

---

## Edge Cases

- **Account status change during active session**: If an account is set to `Inactive` while the user has an active session, the next state-changing operation must revalidate server-side and deny access. The user is redirected to Login. (CU-015, FE-001)

- **Role change during active session**: If a role is removed from an account, the security stamp is updated, invalidating the session. The user must re-authenticate. (CU-015, FE-002)

- **Simultaneous login attempts**: If a user submits multiple login requests concurrently, the system must handle them correctly. The rate limiter and Identity's lockout mechanism work independently.

- **Client-side validation bypass**: If a user disables client-side validation (or uses tools to bypass it), the server must still reject requests with missing or invalid fields. (CU-012, FA-001)

- **Deep-link redirection after authentication**: If a user is redirected to Login from a protected page and authenticates successfully, they should be returned to the originally requested page if it remains valid and authorized. If the page is no longer valid/authorized, they should be redirected to the default dashboard. (CU-012, FA-003)

- **Logout with invalid anti-forgery token**: If a logout POST request is sent without a valid anti-forgery token, the server rejects the request and the session remains active. (CU-014, FE-001)

- **Session reuse after logout**: A session cookie invalidated by logout should not be accepted for any subsequent request. (CU-014, FE-002)

- **Rate limit exceeded before credential verification**: If the rate limit for the login endpoint is exceeded, the system rejects the request (HTTP 429) before verifying credentials, and does not increment `AccessFailedCount`. (CU-013, FE-002)

- **Auto-focus on email field**: The Login page should auto-focus the email field on load to improve UX. (spec_003-screen-construction-guide.md §3.1)

- **Disable submit button on double-click**: The login form should disable the submit button after the first click to prevent double submission. (spec_003-screen-construction-guide.md §3.1)

- **Post/Redirect/Get for login**: After successful login, the system follows PRG pattern: redirect (GET) to the destination page. This prevents accidental duplicate submissions. (Constitution §7.2, spec_003-screen-construction-guide.md)

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of authentication attempts with valid credentials for `Active` accounts result in successful authentication and session creation, verified by integration tests covering all role combinations (User only, Approver only, both roles).

- **SC-002**: 100% of authentication attempts with invalid credentials, inactive accounts, or locked accounts result in the same generic error message, verified by automated tests for each failure category.

- **SC-003**: Rate limiting rejects requests exceeding the configured threshold with HTTP 429, and these rejected requests do not increment `AccessFailedCount`, verified by load/rate-limit tests.

- **SC-004**: Every successful and failed authentication attempt generates an audit record with all required fields populated (actor, timestamp, action, result, internal reason, correlation ID), verified by integration tests.

- **SC-005**: Session cookies are configured with `HttpOnly`, `Secure` (in production), and appropriate `SameSite` policy, verified by configuration and integration tests.

- **SC-006**: Session expiration (idle timeout and absolute lifetime) correctly redirects users to Login with the original URL preserved, verified by automated time-based tests using `TimeProvider` abstraction.

- **SC-007**: Logout invalidates the session cookie and subsequent requests with the old cookie are denied, verified by integration tests.

- **SC-008**: Users with both roles default to the User Dashboard with the Role Switcher available in the sidebar, verified by integration tests.

- **SC-009**: Deep-link redirection after authentication works correctly: users are returned to the originally requested page when valid and authorized, verified by integration tests.

- **SC-010**: Security stamp changes (role or status updates) invalidate existing sessions, requiring re-authentication, verified by integration tests.

---

## Assumptions

- Accounts are pre-provisioned in the system by an external process (HR/IT). There is no account creation, registration, or self-service provisioning in the MVP.
- The authentication database (ASP.NET Core Identity tables) and the business user data (Employee/Approver) are linked through a stable identifier (e.g., `IdentityUser.Id` mapped to `Employee.IdentityId`).
- The `Active`/`Inactive` status is stored in a custom attribute on the IdentityUser or a separate linked entity accessible during authentication.
- Role assignments (`User`, `Approver`) are stored as ASP.NET Core Identity role claims and are pre-provisioned.
- Password policies (minimum length, complexity) are configured according to organizational security policy.
- The system uses a single timezone for all users (Constitution §5.1).
- The production environment uses HTTPS with valid TLS certificates (no self-signed certificates in production).
- The application is deployed with security headers (CSP, HSTS, `X-Content-Type-Options`) configured appropriately.
- The rate limit for the login endpoint is configurable and will be set during deployment (default: 5 attempts per minute per IP, or as configured).
- `MAX_FAILED_ACCESS_ATTEMPTS` and `LOCKOUT_DURATION_MINUTES` are configurable (defaults: 5 attempts, 5 minutes lockout, or as configured).
- The session cookie absolute lifetime (8–12 hours) and idle timeout (20 minutes) are configurable and will be set during deployment.

---

## Cross-Reference

- **Use Cases**: [casos-de-uso-spec_004.md](casos-de-uso-spec_004.md) — Detailed use cases for login, denial, logout, and session expiration.
- **Constitution**: [constitution.md](constitution.md) — v4.0.0, §4 (Actors), §7 (Security), §8 (Auditing).
- **Screen Construction**: [spec_003-screen-construction-guide.md](spec_003-screen-construction-guide.md) — Login screen specification (§3.1), Shared Layout (§3.8), routing.
- **Design System**: [spec_002-frontend-design-system.md](spec_002-frontend-design-system.md) — Design tokens, colors, typography.
- **Vacation Request Management**: [spec_001-vacation-request.md](spec_001-vacation-request.md) — Core business workflows requiring authentication.