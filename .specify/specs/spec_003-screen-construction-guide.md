# Screen Construction Guide — NovaLeave MVP

**Feature Branch**: `003-screen-construction-guide`
**Created**: 2026-07-22
**Last Updated**: 2026-07-24
**Status**: Draft — Ready for Use
**Applies To**: Razor Pages UI (.NET 10)
**Design System Reference**: [spec_002-frontend-design-system.md](spec_002-frontend-design-system.md)

## Overview

This guide defines how every MVP screen is constructed, which reusable components it includes, what HTML structure it follows, what client-side behavior is required, and how it connects to backend controllers and use cases. All screens must comply with the rules established in `spec_002-frontend-design-system.md`.

### Responsive Baseline (Mandatory)

- This spec uses a **mobile-first implementation strategy**: base styles target mobile first, then progressively enhance with `@media (min-width: 768px)` and larger breakpoints from `spec_002`.
- All responsive behavior must be verified in browser responsive emulation and at least one real mobile viewport before screen sign-off.
- Raster images must use responsive sources (`srcset` + `sizes`); all UI images must be fluid (`max-width: 100%; height: auto;`, e.g., `img-fluid` utility class).

---

## 1. Reusable Components

Each component below is implemented as a **Razor partial view** or **Tag Helper** under `Views/Shared/Components/`. Every component must be responsive (using a 12-column grid on desktop/tablet and 4-column on mobile per spec_002), accessible, and styled through design tokens (CSS variables).

### 1.1 StatusBadge

**Partial**: `_StatusBadge.cshtml`

**Purpose**: Renders the visual state of a vacation request.

**HTML Structure**:

```html
<span class="badge badge--{status}" aria-label="Status: {StatusText}">
  {StatusText}
</span>
```

**Variants**: `pending`, `approved`, `rejected`, `cancelled`, `voided`, `expired`

**Rules**:
- Color mapping per `spec_002` semantic state colors.
- Always includes readable text label (never color-only).
- Font: 12px / weight 500 (Labels/Badges token).

---

### 1.2 AlertMessage

**Partial**: `_AlertMessage.cshtml`

**Purpose**: Displays success, warning, error, or info notifications.

**HTML Structure**:

```html
<div class="alert alert--{type}" role="alert" aria-live="polite">
  <span class="alert__icon" aria-hidden="true">{icon}</span>
  <span class="alert__text">{message}</span>
  <button class="alert__dismiss" aria-label="Dismiss">&times;</button>
</div>
```

**Types**: `success`, `warning`, `error`, `info`

**Rules**:
- Auto-dismiss after 5s for success; persistent for errors.
- Fade-out transition per motion spec (180ms ease-out).
- Keyboard-dismissible via Escape.
- Respects `prefers-reduced-motion`: transitions are disabled when the user's system setting is enabled.

---

### 1.3 ConfirmationModal

**Partial**: `_ConfirmationModal.cshtml`

**Purpose**: Requires explicit user confirmation before irreversible actions.

**HTML Structure**:

```html
<div class="modal-overlay" aria-hidden="true">
  <dialog class="modal" role="alertdialog" aria-labelledby="modal-title" aria-describedby="modal-desc">
	<span class="modal__icon" aria-hidden="true">{icon}</span>
	<h2 id="modal-title" class="modal__title">{title}</h2>
	<p id="modal-desc" class="modal__description">{description}</p>
	<div class="modal__actions">
	  <button class="btn btn--secondary" data-action="cancel">Cancel</button>
	  <button class="btn btn--{actionType}" data-action="confirm">{confirmLabel}</button>
	</div>
  </dialog>
</div>
```

**Rules**:
- Focus trap inside modal while open.
- Close on Escape key or Cancel button.
- Confirm button is NOT pre-focused (prevent accidental Enter).
- Transition: opacity fade 280ms ease-out.
- Respects `prefers-reduced-motion`: all transitions are disabled when the user's system setting is enabled.
- Anti-forgery token included in confirmation form submission.
- Icon uses a representative symbol (e.g., ⚠️ for destructive actions, ✅ for approvals) or a corresponding SVG icon; always `aria-hidden="true"`.

---

### 1.4 PaginationControl

**Partial**: `_Pagination.cshtml`

**Purpose**: Server-side pagination for lists exceeding 50 records.

**HTML Structure**:

```html
<nav class="pagination" aria-label="Page navigation">
  <a class="pagination__link pagination__link--prev" href="{prevUrl}" aria-label="Previous page">‹</a>
  <span class="pagination__current" aria-current="page">{currentPage} of {totalPages}</span>
  <a class="pagination__link pagination__link--next" href="{nextUrl}" aria-label="Next page">›</a>
</nav>
```

**Rules**:
- Disabled state for first/last page boundaries.
- Touch-target minimum 44x44px per spec_002 responsive rule.
- Page size fixed at 50 records (server-enforced).

---

### 1.5 FormField

**Partial**: `_FormField.cshtml`

**Purpose**: Standardized input field wrapper with label, validation, and helper text.

**HTML Structure**:

```html
<div class="form-field {hasError ? 'form-field--error' : ''}">
  <label class="form-field__label" for="{fieldId}">
	{label} <span class="form-field__required" aria-label="required">*</span>
  </label>
  <input class="form-field__input" id="{fieldId}" name="{fieldName}" type="{type}" 
		 aria-describedby="{fieldId}-help {fieldId}-error" aria-invalid="{hasError}" />
  <span class="form-field__help" id="{fieldId}-help">{helpText}</span>
  <span class="form-field__error" id="{fieldId}-error" role="alert">{errorMessage}</span>
</div>
```

**Rules**:
- Label always visible (never placeholder-only).
- Error message appears inline below input.
- Required indicator visible and aria-labelled.
- Border color changes to danger red on error.
- Touch target for input fields: minimum 44x44px.

---

### 1.6 BalanceCard

**Partial**: `_BalanceCard.cshtml`

**Purpose**: Shows the employee's current available balance prominently.

**HTML Structure**:

```html
<div class="balance-card" aria-label="Available vacation balance">
  <span class="balance-card__value">{availableDays}</span>
  <span class="balance-card__label">days available</span>
  <span class="balance-card__reserved">({reservedDays} reserved)</span>
</div>
```

**Rules**:
- Always visible on request creation and history pages.
- Updates reflect reserved days (Pending) separately from deducted days.
- Font size: H2 for value (24px / weight 600 per spec_002), body for label.
- Touch-friendly layout: text easily readable at all breakpoints; no horizontal scroll.

---

### 1.7 RequestCard (Mobile Table Alternative)

**Partial**: `_RequestCard.cshtml`

**Purpose**: Card-based representation of a request row for mobile viewports.

**HTML Structure**:

```html
<article class="request-card" aria-label="Vacation request from {startDate} to {endDate}">
  <header class="request-card__header">
	<span class="request-card__dates">{startDate} – {endDate}</span>
	<!-- StatusBadge component -->
  </header>
  <div class="request-card__body">
	<span class="request-card__days">{workingDays} working days</span>
	<span class="request-card__reason">{reason}</span>
  </div>
  <footer class="request-card__actions">
	<!-- Action buttons if applicable -->
  </footer>
</article>
```

**Rules**:
- Replaces table rows on mobile viewports (< 768px per spec_002 tablet/desktop breakpoint).
- Preserves all visible data from the table row.
- Touch-friendly action buttons (minimum 44x44px touch target).

---

### 1.8 EmptyState

**Partial**: `_EmptyState.cshtml`

**Purpose**: Shown when a list or view contains no data.

**HTML Structure**:

```html
<div class="empty-state" role="status">
  <img
	class="empty-state__illustration img-fluid"
	src="{illustrationPathDefault}"
	srcset="{illustrationPathSm} 480w, {illustrationPathMd} 768w, {illustrationPathLg} 1200w"
	sizes="(max-width: 767px) 80vw, 360px"
	alt="" />
  <h3 class="empty-state__title">{title}</h3>
  <p class="empty-state__description">{description}</p>
  <a class="btn btn--primary" href="{actionUrl}">{actionLabel}</a>
</div>
```

**Rules**:
- Always provides a clear next action (CTA button).
- Illustration is decorative (`alt=""`).
- Centered layout on all breakpoints.
- Decorative and content images must use fluid sizing (`img-fluid` or equivalent CSS: `max-width: 100%; height: auto;`).
- Non-SVG/raster illustrations must provide `srcset` and `sizes` for responsive loading.

---

### 1.9 Sidebar

**Partial**: `_Sidebar.cshtml`

**Purpose**: Primary navigation for all authenticated screens. Replaces the traditional top-header navbar.

**HTML Structure**:

```html
<aside class="sidebar" aria-label="Main navigation">
  <div class="sidebar__header">
	<a href="/" class="sidebar__logo" aria-label="NovaLeave home">
	  <img src="~/img/logo.svg" alt="NovaLeave" />
	</a>
  </div>

  <nav class="sidebar__nav">
	<ul class="sidebar__menu">
	  <li class="sidebar__item">
		<a class="sidebar__link sidebar__link--active" href="{url}">
		  <span class="sidebar__icon" aria-hidden="true">{icon}</span>
		  <span class="sidebar__label">{label}</span>
		</a>
	  </li>
	</ul>
  </nav>

  <div class="sidebar__footer">
	<!-- Role Switcher (visible only when user has multiple roles) -->
	<div class="role-switcher" id="role-switcher" style="display:none;">
	  <button class="role-switcher__trigger" aria-haspopup="listbox" aria-expanded="false">
		<span>{activeRoleIcon}</span>
		<span>Viewing as: <strong>{activeRoleLabel}</strong></span>
		<span class="role-switcher__chevron">▾</span>
	  </button>
	</div>

	<span class="sidebar__username">{userName}</span>
	<form method="post" asp-controller="Account" asp-action="Logout">
	  @Html.AntiForgeryToken()
	  <button type="submit" class="btn btn--text">Sign Out</button>
	</form>
  </div>

  <!-- Mobile toggle button (visible only on viewports < 768px) -->
  <button class="sidebar__toggle" aria-label="Toggle navigation" aria-expanded="false">
	<span class="hamburger-icon"></span>
  </button>
</aside>

<!-- Mobile overlay (visible when sidebar is open on mobile) -->
<div class="sidebar__overlay" aria-hidden="true"></div>
```

**Rules**:
- Fixed width: 260px on tablet and desktop (≥768px per spec_002 breakpoint); slides in from the left on mobile (<768px).
- Sticky/fixed position on tablet and desktop; the main content area fills the remaining horizontal space.
- On mobile (<768px): sidebar is hidden by default; a hamburger toggle button reveals it as an overlay panel with a backdrop overlay.
- Active page highlighted with distinct background color and a left border indicator.
- Role-conditional links: Employee sees employee routes, Approver sees approver routes. Users with both roles see both sections.
- Icon next to each label provides quick visual recognition; always `aria-hidden="true"`.
- Collapsible sub-sections for grouped navigation items.
- User information and logout are placed at the bottom (sidebar__footer), always visible.
- The Role Switcher appears in the sidebar footer only for users with multiple roles.
- Mobile drawer uses transform slide-in (280ms ease-out).
- Respects `prefers-reduced-motion`: slide and overlay transitions are disabled when the user's system setting is enabled.

---

## 2. Screen Inventory

### Screen Map

| # | Screen Name | Route | Actor | Priority |
| :--- | :--- | :--- | :--- | :--- |
| 1 | Login | `/Account/Login` | All (unauthenticated) | P1 |
| 2 | Employee Dashboard | `/Employee/Dashboard` | Employee | P1 |
| 3 | New Request | `/Employee/Requests/Create` | Employee | P1 |
| 4 | Request Detail (Employee) | `/Employee/Requests/{id}` | Employee | P1 |
| 5 | Edit Pending Request | `/Employee/Requests/{id}/Edit` | Employee | P2 |
| 6 | Approver Dashboard | `/Approver/Dashboard` | Approver | P1 |
| 7 | Request Detail (Approver) | `/Approver/Requests/{id}` | Approver | P1 |
| 8 | Shared Layout | `_Layout.cshtml` | All (authenticated) | P1 |
| 9 | Not Found (404) | `{*url}` (catch-all) | All | P1 |

---

## 3. Screen Specifications

---

### 3.1 Login

**Route**: `/Account/Login`  
**Actor**: Unauthenticated user  
**Controller**: `AccountController.Login`

#### HTML Structure

```html
<div class="login-page">
  <section class="login-card">
	<header class="login-card__header">
	  <img src="/img/logo.svg" alt="NovaLeave logo" class="login-card__logo" />
	  <h1 class="login-card__title">Sign In</h1>
	</header>
	<form method="post" asp-controller="Account" asp-action="Login" class="login-form">
	  @Html.AntiForgeryToken()
	  <!-- FormField: Email -->
	  <!-- FormField: Password -->
	  <button type="submit" class="btn btn--primary btn--full-width">Sign In</button>
	</form>
  <!-- AlertMessage for login errors -->
  </section>
</div>
```

#### Components Used

- `FormField` × 2 (Email, Password)
- `AlertMessage` (on failed login)

#### JS Behavior — `login-validation.js`

| Function | Behavior |
| :--- | :--- |
| `validateLoginForm()` | Prevent empty submission; show inline errors for missing email/password. |
| `disableOnSubmit()` | Disable submit button after first click to prevent double-submission. |

#### Behavior Rules

- Redirect authenticated users to their role-appropriate dashboard.
- On failed login: show generic error message (do not reveal which field is wrong).
- Auto-focus email field on page load.
- Single-column centered layout on all breakpoints.

---

### 3.2 Employee Dashboard

**Route**: `/Employee/Dashboard`  
**Actor**: Employee (authenticated)  
**Controller**: `EmployeeController.Dashboard`

#### HTML Structure

```html
<div class="dashboard">
  <header class="dashboard__header">
	<h1>My Vacation Requests</h1>
	<a href="/Employee/Requests/Create" class="btn btn--primary">New Request</a>
  </header>

  <!-- BalanceCard component -->

  <section class="dashboard__filters">
	<select name="status" class="filter-select" aria-label="Filter by status">
	  <option value="">All statuses</option>
	  <option value="Pending">Pending</option>
	  <option value="Approved">Approved</option>
	  <option value="Rejected">Rejected</option>
	  <option value="Cancelled">Cancelled</option>
	  <option value="Voided">Voided</option>
	  <option value="Expired">Expired</option>
	</select>
  </section>

	<!-- Desktop/Tablet: Table -->
  <div class="table-responsive request-table-wrap request-table-wrap--desktop" role="region" aria-label="My vacation requests table">
	<table class="request-table request-table--desktop">
	  <thead>
		<tr>
		  <th>Dates</th>
		  <th>Working Days</th>
		  <th>Reason</th>
		  <th>Shipped</th>
		  <th>Status</th>
		  <th>Actions</th>
		</tr>
	  </thead>
	  <tbody>
		<!-- Rows with StatusBadge -->
	  </tbody>
	</table>
  </div>

  <!-- Mobile: RequestCard list -->
  <div class="request-list request-list--mobile">
	<!-- RequestCard components -->
  </div>

  <!-- EmptyState (if no requests) -->
  <!-- PaginationControl -->
</div>
```

#### Components Used

- `BalanceCard`
- `StatusBadge` (per row/card)
- `RequestCard` (mobile breakpoint)
- `PaginationControl`
- `EmptyState`

#### JS Behavior — `employee-dashboard.js`

| Function | Behavior |
| :--- | :--- |
| `filterByStatus()` | Submit filter form on change; reload page with query parameter. |
| `confirmCancel(requestId)` | Open ConfirmationModal before cancelling a Pending request. |
| `confirmVoid(requestId)` | Open ConfirmationModal before voiding an Approved request. |

#### Behavior Rules

- Default sort: most recent first (by creation date).
- Show table on tablet and desktop (≥768px per spec_002 breakpoint), cards on mobile (<768px).
- Table implementation uses a `table-responsive` wrapper with `overflow-x: auto` and `-webkit-overflow-scrolling: touch`; this is a fallback for dense data only (mobile primary pattern remains `RequestCard`, not horizontal table scrolling).
- "Cancel" action visible only for `Pending` requests owned by current user.
- "Void" action visible only for `Approved` requests with future start date.
- Balance card always visible at top.
- Pagination appears only when records > 50.
- Any successful employee cancellation must update the request status in the dashboard to `Cancelled` (or the equivalent approved domain state if the backend uses `Canceled`), and the row/card must reflect the new badge immediately.

---

### 3.3 New Request

**Route**: `/Employee/Requests/Create`  
**Actor**: Employee (authenticated)  
**Controller**: `EmployeeController.CreateRequest` (GET/POST)

#### HTML Structure

```html
<div class="create-request">
  <h1>New Vacation Request</h1>

  <!-- BalanceCard component -->

  <form method="post" asp-controller="Employee" asp-action="CreateRequest" class="request-form" novalidate>
	@Html.AntiForgeryToken()

	<!-- FormField: Start Date (type="date") -->
	<!-- FormField: End Date (type="date") -->

	<div class="request-form__computed" aria-live="polite">
	  <span class="computed-days__label">Working days:</span>
	  <span class="computed-days__value" id="computed-days">—</span>
	</div>

	<!-- FormField: Reason (textarea, required) -->

	<div class="request-form__actions">
	  <a href="/Employee/Dashboard" class="btn btn--secondary">Cancel</a>
	  <button type="submit" class="btn btn--primary">Submit Request</button>
	</div>
  </form>

  <!-- AlertMessage (validation summary) -->
</div>
```

#### Components Used

- `BalanceCard`
- `FormField` × 3 (Start Date, End Date, Reason)
- `AlertMessage` (server validation errors)

#### JS Behavior — `create-request.js`

| Function | Behavior |
| :--- | :--- |
| `validateDates()` | On change of start/end date: check start < end, start > today. Show inline error if invalid. |
| `computeWorkingDaysPreview()` | On valid date range change: compute working days client-side (Monday–Friday count) and display in `#computed-days`. Formula: iterate each day in the range, increment if weekday. This is a preview only; server recalculates authoritatively on submit. |
| `validateBalance()` | Compare computed days with displayed balance; show warning if insufficient. |
| `validateReason()` | On blur: check reason is not empty and within max length. |
| `disableOnSubmit()` | Prevent double-submission. |

#### Behavior Rules

- Start date input: min = tomorrow.
- End date input: min = start date value (dynamic).
- Working days preview updates live but is NOT the authoritative value.
- Server rejects if balance insufficient (client warning is advisory).
- On success: redirect to Employee Dashboard with success AlertMessage.
- On validation failure: re-render form with errors preserved and fields populated.
- Single-column layout on all breakpoints.

---

### 3.4 Request Detail (Employee)

**Route**: `/Employee/Requests/{id}`  
**Actor**: Employee (owner only)  
**Controller**: `EmployeeController.RequestDetail`

#### HTML Structure

```html
<div class="request-detail">
  <h1>Request Detail</h1>

  <section class="request-detail__summary">
	<dl class="detail-list">
	  <dt>Status</dt>
	  <dd><!-- StatusBadge --></dd>
	  <dt>Start Date</dt>
	  <dd>{startDate}</dd>
	  <dt>End Date</dt>
	  <dd>{endDate}</dd>
	  <dt>Working Days</dt>
	  <dd>{workingDays}</dd>
	  <dt>Reason</dt>
	  <dd>{reason}</dd>
	  <dt>Submitted</dt>
	  <dd>{createdAt}</dd>
	  <dt>Expires</dt>
	  <dd>{expiryDate}</dd>
	  <dt>Resolved By</dt>
	  <dd>{resolvedBy || "—"}</dd>
	  <dt>Rejection Reason</dt>
	  <dd>{rejectionReason || "—"}</dd>
	</dl>
  </section>

  <section class="request-detail__actions">
	<!-- Conditional: Cancel button if Pending -->
	<!-- Conditional: Void button if Approved + future start -->
	<!-- Conditional: Edit link if Pending -->
	<a href="/Employee/Dashboard" class="btn btn--secondary">Back to Dashboard</a>
  </section>

  <!-- ConfirmationModal (for cancel/void) -->
</div>
```

#### Components Used

- `StatusBadge`
- `ConfirmationModal` (cancel/void actions)
- `AlertMessage` (action result feedback)

#### JS Behavior — `request-detail.js`

| Function | Behavior |
| :--- | :--- |
| `confirmCancel(requestId)` | Opens modal with warning: "This action is irreversible. The reserved days will be released." |
| `confirmVoid(requestId)` | Opens modal with warning: "This will return {days} days to your balance. This action cannot be undone." |

#### Behavior Rules

- Only show actions applicable to the current state.
- Rejection reason section hidden when not applicable.
- If request does not belong to the user or does not exist: return 404 (existence hiding per SR-003).

---

### 3.5 Edit Pending Request

**Route**: `/Employee/Requests/{id}/Edit`  
**Actor**: Employee (owner, Pending state only)  
**Controller**: `EmployeeController.EditRequest` (GET/POST)

#### HTML Structure

```html
<div class="edit-request">
  <h1>Edit Request</h1>

  <!-- BalanceCard component -->

  <form method="post" asp-controller="Employee" asp-action="EditRequest" class="request-form" novalidate>
	@Html.AntiForgeryToken()
	<input type="hidden" name="id" value="{requestId}" />
	<input type="hidden" name="concurrencyToken" value="{rowVersion}" />

	<!-- FormField: Start Date (pre-filled) -->
	<!-- FormField: End Date (pre-filled) -->

	<div class="request-form__computed" aria-live="polite">
	  <span class="computed-days__label">Working days:</span>
	  <span class="computed-days__value" id="computed-days">{currentDays}</span>
	</div>

	<!-- FormField: Reason (pre-filled) -->

	<div class="request-form__actions">
	  <a href="/Employee/Requests/{id}" class="btn btn--secondary">Cancel</a>
	  <button type="submit" class="btn btn--primary">Save Changes</button>
	</div>
  </form>

  <!-- AlertMessage -->
</div>
```

#### Components Used

- `BalanceCard`
- `FormField` × 3
- `AlertMessage`

#### JS Behavior — `edit-request.js`

Same as `create-request.js`:

| Function | Behavior |
| :--- | :--- |
| `validateDates()` | Same rules as create. |
| `computeWorkingDaysPreview()` | Same client-side computation as create (Monday–Friday count). |
| `validateBalance()` | Must account for the days already reserved by this request. |
| `validateReason()` | Same rules. |
| `disableOnSubmit()` | Prevent double-submission. |

#### Behavior Rules

- Only accessible if request is in `Pending` state and owned by current user.
- Concurrency token prevents lost updates.
- On success: redirect to Request Detail with success message.
- On concurrency conflict: show error explaining the request was modified elsewhere.
- If request is no longer Pending: redirect to detail with error.

---

### 3.6 Approver Dashboard

**Route**: `/Approver/Dashboard`  
**Actor**: Approver (authenticated)  
**Controller**: `ApproverController.Dashboard`

#### HTML Structure

```html
<div class="approver-dashboard">
  <h1>Pending Approvals</h1>

  <section class="approver-dashboard__summary">
	<div class="stat-card">
	  <span class="stat-card__value">{pendingCount}</span>
	  <span class="stat-card__label">Pending requests</span>
	</div>
	<div class="stat-card stat-card--urgent">
	  <span class="stat-card__value">{urgentCount}</span>
	  <span class="stat-card__label">Expiring soon</span>
	</div>
  </section>

	<!-- Desktop/Tablet: Table -->
  <div class="table-responsive request-table-wrap request-table-wrap--desktop" role="region" aria-label="Pending approvals table">
	<table class="request-table request-table--desktop">
	  <thead>
		<tr>
		  <th>Employee</th>
		  <th>Dates</th>
		  <th>Working Days</th>
		  <th>Shipped</th>
		  <th>Status</th>
		  <th>Expires In</th>
		  <th>Actions</th>
		</tr>
	  </thead>
	  <tbody>
		<!-- Rows sorted by urgency -->
	  </tbody>
	</table>
  </div>

  <!-- Mobile: RequestCard list (approver variant) -->
  <div class="request-list request-list--mobile">
	<!-- RequestCard components with employee name -->
  </div>

  <!-- EmptyState (if no pending requests) -->
  <!-- PaginationControl -->
</div>
```

#### Components Used

- `StatusBadge` (urgency indicator)
- `RequestCard` (mobile, approver variant showing employee name)
- `PaginationControl`
- `EmptyState`

#### JS Behavior — `approver-dashboard.js`

| Function | Behavior |
| :--- | :--- |
| `highlightUrgent()` | Apply urgency styling (orange border) to requests within 2 days of expiry. |

#### Behavior Rules

- Default sort: requests closest to expiry first (urgency priority).
- Show table on tablet and desktop (≥768px per spec_002 breakpoint), cards on mobile (<768px).
- Table implementation uses a `table-responsive` wrapper with `overflow-x: auto` and `-webkit-overflow-scrolling: touch`; horizontal scroll is fallback-only and must not replace the mobile card pattern.
- Show employee name, dates, working days, submission date, and days until expiry.
- "Review" link opens Approver Request Detail.
- Only shows requests from employees currently assigned to this approver.
- Empty state: "No pending requests to review."

---

### 3.7 Request Detail (Approver)

**Route**: `/Approver/Requests/{id}`  
**Actor**: Approver (assigned to request owner)  
**Controller**: `ApproverController.RequestDetail`

#### HTML Structure

```html
<div class="request-detail request-detail--approver">
  <h1>Review Request</h1>

  <section class="request-detail__employee-info">
	<dl class="detail-list">
	  <dt>Employee</dt>
	  <dd>{employeeName}</dd>
	  <dt>Current Balance</dt>
	  <dd>{employeeBalance} days</dd>
	</dl>
  </section>

  <section class="request-detail__summary">
	<dl class="detail-list">
	  <dt>Status</dt>
	  <dd><!-- StatusBadge --></dd>
	  <dt>Start Date</dt>
	  <dd>{startDate}</dd>
	  <dt>End Date</dt>
	  <dd>{endDate}</dd>
	  <dt>Working Days</dt>
	  <dd>{workingDays}</dd>
	  <dt>Reason</dt>
	  <dd>{reason}</dd>
	  <dt>Shipped</dt>
	  <dd>{createdAt}</dd>
	  <dt>Expires</dt>
	  <dd>{expiryDate}</dd>
	</dl>
  </section>

  <section class="request-detail__decision">
	<form method="post" asp-controller="Approver" asp-action="Approve" class="decision-form">
	  @Html.AntiForgeryToken()
	  <input type="hidden" name="requestId" value="{id}" />
	  <input type="hidden" name="concurrencyToken" value="{rowVersion}" />
	  <button type="button" class="btn btn--primary" data-action="approve">Approve</button>
	</form>

	<form method="post" asp-controller="Approver" asp-action="Reject" class="decision-form">
	  @Html.AntiForgeryToken()
	  <input type="hidden" name="requestId" value="{id}" />
	  <input type="hidden" name="concurrencyToken" value="{rowVersion}" />
	  <!-- FormField: Rejection Reason (required, shown on reject intent) -->
	  <button type="button" class="btn btn--danger" data-action="reject">Reject</button>
	</form>
  </section>

  <!-- ConfirmationModal (approve) -->
  <!-- ConfirmationModal (reject) -->

  <a href="/Approver/Dashboard" class="btn btn--secondary">Back to Dashboard</a>
</div>
```

#### Components Used

- `StatusBadge`
- `FormField` (Rejection Reason — shown conditionally)
- `ConfirmationModal` × 2 (approve, reject)
- `AlertMessage`

#### JS Behavior — `approver-decision.js`

| Function | Behavior |
| :--- | :--- |
| `showRejectReason()` | On "Reject" click: reveal rejection reason field and set focus. |
| `validateRejectReason()` | Ensure reason is provided before allowing confirmation. |
| `confirmApprove(requestId)` | Open modal: "This will deduct {days} days from {employee}'s balance. This action is final." |
| `confirmReject(requestId)` | Open modal: "This request will be permanently rejected. The employee will see your reason." |
| `disableOnSubmit()` | Prevent double-submission on confirm. |

#### Behavior Rules

- Cannot approve/reject own request (button hidden + server-enforced).
- Rejection reason field hidden by default; revealed only on reject intent.
- Rejection reason is mandatory — reject confirmation disabled until provided.
- Any state-changing action must update the originating dashboard immediately after success, without requiring a full page reload.
- Approving a Pending request must change its dashboard status to Approved; rejecting it must change the status to Rejected.
- Editing a Pending request must refresh the employee dashboard row/card so the latest dates, reason, and status-related actions are visible.
- On success: redirect to Approver Dashboard with success AlertMessage.
- On concurrency conflict: show error (request was already resolved).
- If request not in Pending state: show read-only detail without action buttons.

---

### 3.8 Shared Layout

**File**: `Views/Shared/_Layout.cshtml`  
**Actor**: All authenticated users

#### HTML Structure

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>@ViewData["Title"] — NovaLeave</title>
  <link rel="stylesheet" href="~/css/tokens.css" />
  <link rel="stylesheet" href="~/css/site.css" />
</head>
<body>
  <div class="app-layout">
	<!-- Sidebar component (primary navigation) -->
	@await Html.PartialAsync("_Sidebar")

	<!-- Mobile overlay backdrop -->
	<div class="sidebar__overlay" aria-hidden="true"></div>

	<main class="site-content">
	  @RenderBody()
	</main>
  </div>

  <footer class="site-footer">
	<p>&copy; {year} Novacomp — NovaLeave</p>
  </footer>

  <script src="~/js/shared.js"></script>
  @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

#### JS Behavior — `shared.js`

| Function | Behavior |
| :--- | :--- |
| `toggleSidebar()` | Open/close the sidebar on mobile; toggle `aria-expanded` on the toggle button and `aria-hidden` on the overlay. On desktop this is a no-op. |
| `closeSidebarOnResize()` | Auto-close sidebar when viewport reaches tablet size or above (≥768px); prevents sticky open state after rotate. |
| `closeSidebarOnOverlayClick()` | Close sidebar when the overlay backdrop is clicked. |
| `initAlertDismiss()` | Attach dismiss handlers to all AlertMessage components. |
| `initModals()` | Register focus-trap, Escape-close, and confirm/cancel handlers for all modals. |
| `toggleRoleSwitcher(event)` | Open/close the Role Switcher menu in the sidebar footer; update `aria-expanded` and keep clicks inside the component from closing it. |
| `switchRole(role)` | Change the active UI role, update the label/checkmarks, rebuild sidebar navigation links, and navigate to the selected role dashboard. |
| `closeRoleSwitcher()` | Close the Role Switcher and reset `aria-expanded` to false. |
| `highlightActiveLink()` | Apply `sidebar__link--active` to the link matching the current page route. |

#### Behavior Rules

- The sidebar is the **only navigation element** on authenticated screens. No top header bar exists.
- The Login and 404 screens use a separate layout (no sidebar).
- Sidebar links are role-conditional (Employee sees employee routes, Approver sees approver routes). Users with both roles see both sections.
- Active page highlighted in the sidebar with a left border accent and bold text.
- On tablet and desktop (≥768px per spec_002 breakpoint): sidebar is always visible, fixed on the left (260px). The `site-content` area fills the remaining width.
- On mobile (<768px): sidebar is hidden by default. The hamburger button (inside the sidebar header) toggles it as an overlay panel with a semi-transparent backdrop.
- The sidebar closes automatically when the viewport resizes to tablet size or above (≥768px), when the overlay is clicked, or when Escape is pressed.
- User information, Role Switcher, and the Sign Out button are fixed at the bottom of the sidebar.
- The Role Switcher is visible only for users with multiple roles; it changes the active **view context** only (does not change authentication or permissions).
- Switching role navigates to the corresponding role dashboard and rebuilds the sidebar links.
- Logout requires anti-forgery token (POST only).
- The `.app-layout` container uses a two-column grid: sidebar (260px) + main content (1fr) on tablet/desktop (≥768px); stacked single-column on mobile (<768px) per spec_002 grid rules.

#### Role Switcher Implementation Notes

**Technical Implementation**:
- The Role Switcher is implemented as a **ViewComponent** (`RoleSwitcherViewComponent.cs`) in `src/NovaLeave.Presentation.Web/ViewComponents/`.
- View location: `Views/Shared/Components/RoleSwitcher/Default.cshtml`
- Invoked via `@await Component.InvokeAsync("RoleSwitcher")` in the sidebar footer of `_Layout.cshtml`.

**Critical Implementation Detail - Inline JavaScript**:
- ViewComponents **CANNOT** use Razor sections (`@section Scripts`). Any JavaScript must be included **inline** within the ViewComponent view.
- The Role Switcher JavaScript must use an **Immediately Invoked Function Expression (IIFE)** pattern:
  ```javascript
  <script>
      (function() {
          // JavaScript code here executes immediately when component renders
          const trigger = document.getElementById('roleSwitcherTrigger');
          // ... event handlers ...
      })();
  </script>
  ```
- This ensures the JavaScript executes as soon as the component is rendered, without depending on `@RenderSection("Scripts")`.

**Role Determination Logic**:
- `RoleSwitcherViewModel.GetActiveRole()` determines the current active role based on the URL path:
  - Paths starting with `/Employee` → "User" role active
  - Paths starting with `/Approver` → "Approver" role active
  - Default: "User" role (preferred for dual-role users)
- `GetRoleDashboard(role)` returns the target dashboard URL:
  - "User" → `/Employee/Dashboard`
  - "Approver" → `/Approver/Dashboard`

**Navigation Behavior**:
- Clicking a role option navigates to the corresponding dashboard via `window.location.href`.
- The sidebar navigation links are automatically filtered by the layout based on the current route (role context).
- No server-side session state is changed; the active role is determined client-side based on the current URL.

**Styling**:
- Role Switcher styles are defined **inline** within the ViewComponent view (in a `<style>` block).
- Styles include dropdown positioning (bottom-up from sidebar footer), hover states, active role highlighting, and ARIA-compliant visual indicators.
- Respects spec_002 design tokens for colors, spacing, and transitions.

**Accessibility**:
- Uses `aria-haspopup="listbox"`, `aria-expanded`, `aria-selected`, and `role="option"` for screen reader compatibility.
- Keyboard navigation: Escape key closes the menu, focus returns to trigger button.
- Click outside to close behavior implemented with document-level event listener.

---

### 3.9 Not Found (404)

**Route**: `{*url}` (catch-all handled by status code middleware re-executing to `/Home/Error`)  
**Actor**: All (unauthenticated + authenticated)  
**Controller**: `HomeController.Error`

#### HTML Structure

```html
<div class="error-page">
  <section class="error-card">
	<h1 class="error-card__code">404</h1>
	<p class="error-card__title">Page Not Found</p>
	<p class="error-card__description">
	  The page you requested could not be found. It may have been moved, deleted, or the URL may be incorrect.
	</p>
  <a href="/" class="btn btn--primary">Go to Home</a>
  </section>
</div>
```

#### Components Used

None (standalone page).

#### JS Behavior — `error-page.js`

| Function | Behavior |
| :--- | :--- |
| `trackNotFound()` | Log the 404 occurrence (URL + referrer) to analytics or server-side logging. |

#### Behavior Rules

- Uses the **unauthenticated shared layout** (no nav, no user context) to avoid confusion when session is invalid.
- Returns HTTP status 404 with the corresponding page content.
- Page is static; no form submission or dynamic content.
- "Go to Home" button redirects to `/` which role-redirects to the appropriate dashboard or login page.
- For authenticated users encountering 404 on their own domain: still shown the same page to prevent information leaking (path existence hiding per SR-003).
- Single-column centered layout on all breakpoints.

---

### 3.10 Screen Navigation Flow

#### Navigation Map

| From | Action / Trigger | To |
| :--- | :--- | :--- |
| Login | Successful authentication | Role-appropriate Dashboard (Employee or Approver) |
| Login | Brand/logo click | Login (same page — unauthenticated) |
| Employee Dashboard | "New Request" button | New Request |
| Employee Dashboard | "Ver" link on a row/card | Request Detail (Employee) |
| Employee Dashboard | "Cancelar" button on Pending row | ConfirmationModal → stays on Dashboard |
| Employee Dashboard | "Anular" button on Approved row | ConfirmationModal → stays on Dashboard |
| Employee Dashboard | Status filter change | Stays on Dashboard (reloads with query param) |
| Employee Dashboard | Brand/logo click | Employee Dashboard |
| New Request | "Cancel" link | Employee Dashboard |
| New Request | Successful submission | Employee Dashboard (with success alert) |
| New Request | Validation failure | New Request (stays on form, errors shown) |
| Request Detail (Employee) | "Back to Dashboard" link | Employee Dashboard |
| Request Detail (Employee) | "Edit" link | Edit Pending Request |
| Request Detail (Employee) | Cancel/Void action | ConfirmationModal → stays on Detail |
| Edit Pending Request | "Cancel" link | Request Detail (Employee) |
| Edit Pending Request | Successful save | Request Detail (Employee) (with success alert) |
| Edit Pending Request | Not Pending or not owner | Redirect to Request Detail (Employee) with error |
| Approver Dashboard | "Review" / "Revisar" link | Request Detail (Approver) |
| Approver Dashboard | Successful approve/reject | Approver Dashboard (with success alert, row updated) |
| Request Detail (Approver) | "Back to Dashboard" link | Approver Dashboard |
| Request Detail (Approver) | Approve action | ConfirmationModal → Approver Dashboard (on confirm) |
| Request Detail (Approver) | Reject action | Reveal reason field → ConfirmationModal → Approver Dashboard |
| Any authenticated page | "Sign Out" button | Login |
| Any unmatched route | Catch-all middleware | Not Found (404) |
| Not Found (404) | "Go to Home" link | `/` (role-redirects to Dashboard or Login) |

#### Navigation Rules

- **Role-based redirect after login**: The system evaluates the authenticated user's role and redirects to the corresponding dashboard. If the user has both roles, the Employee Dashboard is the default.
- **Direct URL access**: Navigating to a URL directly follows the same flow as clicking the corresponding navigation element, subject to authorization checks.
- **Authorization enforcement**: If a user navigates to a screen they are not authorized for (e.g., an employee accessing `/Approver/Dashboard`), the system returns 404 (existence hiding per SR-003), not 403.
- **Back navigation**: The browser's back button must restore the previous screen state. No `window.history.replaceState` manipulation that breaks expected browser behavior.
- **Session expiry**: If the session expires during use, the next action redirects to Login. After successful re-authentication, redirect to the originally requested page if still valid.
- **404 catch-all**: Any route that does not match a defined controller action renders the Not Found screen with HTTP status 404.

---

## 4. Route and Controller Summary

### Employee Routes

| Method | Route | Controller Action | Screen | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| GET | `/Employee/Dashboard` | `EmployeeController.Dashboard` | Employee Dashboard | List own requests + balance |
| GET | `/Employee/Requests/Create` | `EmployeeController.CreateRequest` | New Request | Show create form |
| POST | `/Employee/Requests/Create` | `EmployeeController.CreateRequest` | New Request | Submit new request |
| GET | `/Employee/Requests/{id}` | `EmployeeController.RequestDetail` | Request Detail (Employee) | View single request detail |
| GET | `/Employee/Requests/{id}/Edit` | `EmployeeController.EditRequest` | Edit Pending Request | Show edit form (Pending only) |
| POST | `/Employee/Requests/{id}/Edit` | `EmployeeController.EditRequest` | Edit Pending Request | Submit edit (Pending only) |
| POST | `/Employee/Requests/{id}/Cancel` | `EmployeeController.CancelRequest` | Request Detail (Employee) | Cancel Pending request |
| POST | `/Employee/Requests/{id}/Void` | `EmployeeController.VoidRequest` | Request Detail (Employee) | Void Approved request |

### Approver Routes

| Method | Route | Controller Action | Screen | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| GET | `/Approver/Dashboard` | `ApproverController.Dashboard` | Approver Dashboard | List pending requests to review |
| GET | `/Approver/Requests/{id}` | `ApproverController.RequestDetail` | Request Detail (Approver) | Review request detail |
| POST | `/Approver/Requests/{id}/Approve` | `ApproverController.Approve` | Request Detail (Approver) | Approve request |
| POST | `/Approver/Requests/{id}/Reject` | `ApproverController.Reject` | Request Detail (Approver) | Reject request (with reason) |

### Account Routes

| Method | Route | Controller Action | Screen | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| GET | `/Account/Login` | `AccountController.Login` | Login | Show login form |
| POST | `/Account/Login` | `AccountController.Login` | Login | Authenticate user |
| POST | `/Account/Logout` | `AccountController.Logout` | Shared Layout | Sign out + invalidate session |

### Error Routes

| Method | Route | Controller Action | Screen | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| GET | `{*url}` (catch-all) | `HomeController.Error` | Not Found (404) | Show 404 page for unmatched routes |

---

## 5. Implementation Checklist

### 5.1 System Foundation

- [ ] Create `tokens.css` with all design system CSS variables (colors, typography, spacing, motion).
- [ ] Create `site.css` with base layout, grid system, and responsive utilities.
- [ ] Implement `_Layout.cshtml` shared layout with responsive navigation.
- [ ] Implement `shared.js` (mobile nav, alert dismiss, modal handlers).
- [ ] Create all reusable partial views:
  - [ ] `_StatusBadge.cshtml`
  - [ ] `_AlertMessage.cshtml`
  - [ ] `_ConfirmationModal.cshtml`
  - [ ] `_Pagination.cshtml`
  - [ ] `_FormField.cshtml`
  - [ ] `_BalanceCard.cshtml`
  - [ ] `_RequestCard.cshtml`
  - [ ] `_EmptyState.cshtml`
  - [ ] `_Sidebar.cshtml`
- [ ] Configure anti-forgery token globally.
- [ ] Set up ASP.NET Core Identity with secure cookie configuration.
- [ ] Verify responsive behavior of layout at all breakpoints.
- [ ] Verify responsive behavior using browser DevTools viewport emulation at minimum widths: 360px, 390px, 768px, 1024px, and 1440px.
- [ ] Verify responsive behavior on at least one physical mobile device (or cloud device lab equivalent) for touch interactions and text readability.
- [ ] Verify keyboard navigation across shared components.
- [ ] Verify `prefers-reduced-motion` support in all animated components.
- [ ] Verify responsive images: no overflow, correct `srcset` candidate selection, and fluid scaling (`img-fluid` or equivalent).

### 5.2 Login Screen

- [ ] Implement `AccountController.Login` (GET + POST).
- [ ] Create Login view with FormField components.
- [ ] Implement `login-validation.js`.
- [ ] Test: failed login shows generic error.
- [ ] Test: successful login redirects to role-appropriate dashboard.
- [ ] Test: already-authenticated user redirected away from login.
- [ ] Verify responsive layout (mobile/tablet/desktop).

### 5.3 Employee Dashboard

- [ ] Implement `EmployeeController.Dashboard` with pagination and status filter.
- [ ] Create Dashboard view with table (desktop) and cards (mobile).
- [ ] Integrate `BalanceCard`, `StatusBadge`, `PaginationControl`, `EmptyState`.
- [ ] Implement `employee-dashboard.js` (filter, cancel/void confirmation).
- [ ] Test: only own requests visible.
- [ ] Test: pagination works at > 50 records.
- [ ] Test: cancel action only on Pending requests.
- [ ] Test: void action only on Approved + future start.
- [ ] Verify responsive switch (table ↔ cards) at the tablet breakpoint (≥768px per spec_002).

### 5.4 New Request Screen

- [ ] Implement `EmployeeController.CreateRequest` (GET + POST).
- [ ] Create view with date fields, reason, balance card, computed days preview.
- [ ] Implement `create-request.js` (validation, preview, double-submit prevention).
- [ ] Implement `calcWorkDays()` helper in `create-request.js` (client-side Monday–Friday count for preview only).
- [ ] Test: server rejects past/today start dates.
- [ ] Test: server rejects start > end.
- [ ] Test: server rejects insufficient balance.
- [ ] Test: server rejects overlapping dates.
- [ ] Test: success redirects to dashboard with message.
- [ ] Verify responsive single-column layout.

### 5.5 Request Detail (Employee)

- [ ] Implement `EmployeeController.RequestDetail`.
- [ ] Create detail view with conditional actions.
- [ ] Implement `request-detail.js` (cancel/void modals).
- [ ] Test: 404 for non-owned or non-existent requests.
- [ ] Test: cancel transitions Pending → Cancelled.
- [ ] Test: void transitions Approved → Voided (future start only).
- [ ] Test: no actions shown for final-state requests.

### 5.6 Edit Pending Request

- [ ] Implement `EmployeeController.EditRequest` (GET + POST).
- [ ] Create edit view (pre-filled form + concurrency token).
- [ ] Implement `edit-request.js`.
- [ ] Test: only accessible for owned Pending requests.
- [ ] Test: concurrency conflict handled gracefully.
- [ ] Test: re-validates all business rules on submit.
- [ ] Test: success redirects to detail with message.

### 5.7 Approver Dashboard

- [ ] Implement `ApproverController.Dashboard` with urgency sorting.
- [ ] Create view with pending list, urgency indicators, stat cards.
- [ ] Integrate `RequestCard` (mobile), `PaginationControl`, `EmptyState`.
- [ ] Implement `approver-dashboard.js` (urgency highlighting).
- [ ] Test: only assigned employees' requests visible.
- [ ] Test: sorted by expiry proximity.
- [ ] Test: pagination works correctly.
- [ ] Verify responsive behavior.

### 5.8 Request Detail (Approver)

- [ ] Implement `ApproverController.RequestDetail`.
- [ ] Implement `ApproverController.Approve` (POST).
- [ ] Implement `ApproverController.Reject` (POST).
- [ ] Create review view with employee info, request summary, decision forms.
- [ ] Implement `approver-decision.js` (reject reason reveal, confirmations).
- [ ] Test: cannot approve/reject own request.
- [ ] Test: cannot act on non-assigned employee's request (404).
- [ ] Test: rejection reason mandatory.
- [ ] Test: approval deducts balance atomically.
- [ ] Test: concurrency conflict handled.
- [ ] Test: already-resolved request shows read-only detail.
- [ ] Verify responsive layout and modal behavior on mobile.

### 5.9 Error / 404 Screen

- [ ] Configure `UseStatusCodePagesWithReExecute` (or equivalent) in `Program.cs` to re-route 404s to `/Home/Error`.
- [ ] Implement `HomeController.Error` (GET) — returns 404 status code + view.
- [ ] Create Error view with centered layout, status code display, and "Go to Home" button.
- [ ] Implement `error-page.js` (analytics logging).
- [ ] Test: navigating to an invalid route returns 404 page (not a generic browser error).
- [ ] Test: 404 returns HTTP status 404 in response headers.
- [ ] Test: 404 page renders correctly for both authenticated and unauthenticated users.
- [ ] Verify responsive centered layout on all breakpoints.

---

## 6. Cross-Reference

- **Design tokens, color, typography, responsive rules**: [spec_002-frontend-design-system.md](spec_002-frontend-design-system.md)
- **Business rules, state machine, functional requirements**: [spec_001-vacation-request.md](spec_001-vacation-request.md)
- **Architecture, controllers, layers**: Constitution v4.0.0 §2–§3

---

## Change Governance

- New screens must be added to this guide before implementation begins.
- Changes to component HTML structure or JS behavior must update this document.
- Route changes must update the Route and Controller Summary table.
