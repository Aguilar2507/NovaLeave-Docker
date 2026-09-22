# Frontend Design Specification — NovaLeave MVP

**Feature Branch**: `002-frontend-design-system`
**Created**: 2026-07-22
**Last Updated**: 2026-07-22
**Status**: Draft — Ready for Use
**Applies To**: Razor Pages UI (.NET 10)

## Overview

This specification defines the visual, interaction, and responsive rules for the NovaLeave frontend. It standardizes UI behavior across all Razor Pages screens to ensure consistency, accessibility, and a high-quality UX.

This document is normative for MVP frontend work: new pages and components must follow these rules unless a later approved design decision supersedes them.

---

## Design Goals

- Build a fully responsive experience across desktop, tablet, and mobile.
- Maintain a consistent visual identity aligned with Novacomp brand colors.
- Improve clarity, usability, and task completion speed for key workflows.
- Keep UI implementation maintainable through reusable tokens and component rules.

---

## Core UX/UI Principles

1. **Clarity first**: Primary actions, status, and next steps must be obvious.
2. **Consistency**: Same action = same visual pattern and interaction behavior.
3. **Feedback always**: Every user action must return clear success, warning, or error feedback.
4. **Prevention over correction**: Use validation, confirmations, and constraints to avoid mistakes.
5. **Accessibility by default**: Keyboard, contrast, focus states, and semantic markup are mandatory.
6. **Responsive-first**: Every screen must work at all breakpoints without functional loss.

---

## Color System

### Primary Colors

| Color | Hex | Primary Use |
| :--- | :--- | :--- |
| Azul Profundo Novacomp | `#0B1A33` | Navigation background, headers, high hierarchy surfaces |
| Azul Corporativo | `#1A3B6B` | Primary buttons, links, key accents |
| Azul Claro | `#4A8BCF` | Hover states, interactive icon accents, secondary emphasis |

### Neutral Colors

| Color | Hex | Primary Use |
| :--- | :--- | :--- |
| White | `#FFFFFF` | Main content backgrounds, cards, text over dark surfaces |
| Light Gray | `#F8F9FA` | Alternating section backgrounds, subtle backgrounds |
| Medium Gray | `#E9ECEF` | Table borders, separators, input backgrounds/borders |
| Text Gray | `#6C757D` | Secondary text, helper text, placeholders |
| Dark Gray | `#343A40` | Primary text on light backgrounds |

### Semantic State Colors

| State | Color | Hex | Usage |
| :--- | :--- | :--- | :--- |
| Success (Approved) | Green | `#28A745` | Approved badges, success messages |
| Warning (Pending) | Yellow | `#FFC107` | Pending badges, expiry-near warnings |
| Danger (Rejected) | Red | `#DC3545` | Rejected badges, error states/messages |
| Info (Cancelled / Expired / Voided) | Blue Gray | `#6C8BA0` | Informational badges for closed states |
| Urgency | Orange | `#FD7E14` | Requests close to expiry indicators |

### Color Usage Rules

- Semantic status colors must not be repurposed for unrelated meanings.
- Do not use color as the only signal: always pair with text/icon labels.
- Ensure text/background combinations comply with WCAG AA contrast.
- Primary call-to-action per page should use **Azul Corporativo**.

---

## Typography

Preferred font family: **Inter** (fallback: **Roboto**, then system sans-serif).

| Element | Font | Size | Weight |
| :--- | :--- | :--- | :--- |
| H1 Titles | Inter / Roboto | `32px` | `700` |
| H2 Titles | Inter / Roboto | `24px` | `600` |
| H3 Titles | Inter / Roboto | `20px` | `600` |
| Body Text | Inter / Roboto | `16px` | `400` |
| Small Text | Inter / Roboto | `14px` | `400` |
| Labels / Badges | Inter / Roboto | `12px` | `500` |

### Typography Rules

- Use sentence case for most labels/titles; avoid full uppercase blocks.
- Keep line-length readable (target ~60–90 chars on desktop body text).
- Reserve H1 for page title only (one H1 per page).
- Ensure minimum body size of 16px on mobile for readability.

---

## Spacing, Layout, and Grid

### Spacing Scale

Use an 8px baseline system:

- `4px`, `8px`, `12px`, `16px`, `24px`, `32px`, `40px`, `48px`

### Layout Rules

- Use a 12-column grid on desktop/tablet and 4 columns on mobile.
- Keep consistent internal padding in cards/forms/tables.
- Prefer vertical rhythm and clear grouping over dense packing.

---

## Responsive Requirements (Mandatory)

The project is **fully responsive in its entirety**. No page or critical workflow may require horizontal scrolling at standard viewport sizes.

### Breakpoints

- **Mobile**: `< 576px`
- **Large Mobile / Small Tablet**: `576px – 767px`
- **Tablet**: `768px – 991px`
- **Desktop**: `992px – 1199px`
- **Wide Desktop**: `>= 1200px`

### Responsive Behavior Rules

- Navigation must adapt to a compact pattern on mobile.
- Tables must degrade gracefully (stacked cards, horizontal scroll container, or key-column priority mode).
- Forms must use single-column layout on mobile and can split in multi-column layouts on desktop.
- Touch targets must be at least 44x44px.
- Modals/dialogs must fit mobile viewport with scroll-safe content.

---

## Component Rules

### Buttons

- Primary action: `#1A3B6B` background, white text.
- Hover: use `#4A8BCF` (or tone-consistent variant).
- Disabled states must be visually distinct and non-clickable.
- Use one primary button per section to avoid action ambiguity.

### Inputs and Forms

- Labels are always visible (do not rely only on placeholder text).
- Field errors are shown inline and summarized at form top when needed.
- Required fields must be clearly marked.
- Date ranges must provide clear format guidance and error feedback.

### Badges and Status Chips

- Must map directly to workflow states (`Pending`, `Approved`, `Rejected`, `Cancelled`, `Voided`, `Expired`).
- Include clear text label plus semantic color.
- Keep visual style consistent in all lists and detail views.

### Tables and Lists

- Header row always visible on desktop.
- Consistent column ordering for request history and approval queues.
- Use pagination for long lists.
- Preserve readable spacing and avoid text clipping.

### Alerts and Feedback

- Success, warning, and error alerts must use semantic colors and clear copy.
- Irreversible actions (approve/reject/cancel/void) must show explicit confirmation.
- Empty states must include explanation and a clear next action.

---

## Accessibility Requirements (Mandatory)

- WCAG 2.1 AA contrast minimum for text and interactive controls.
- Full keyboard navigation for all interactive UI.
- Visible focus indicator for links, buttons, fields, and custom controls.
- Semantic HTML and accessible labels for form controls.
- All informative images must include meaningful `alt` text that conveys the purpose or content of the image.
- Decorative images must use empty `alt=""` so assistive technologies can ignore them.
- Error messages must be understandable and programmatically associated with fields.

---

## UX Rules for MVP Flows

### Employee Request Creation

- Show available balance before and during date selection.
- Validate date range and constraints immediately after input.
- Display computed working days as read-only derived value.

### Approver Decision Flow

- Prioritize pending requests by urgency and date proximity.
- Require rejection reason before enabling reject confirmation.
- Emphasize finality and consequences of approve/reject actions.

### Employee History

- Default sort by most recent request.
- Keep filters simple (status/date) and resettable.
- Ensure status visibility is immediate without opening detail view.

---

## Frontend Implementation Rules (Razor Pages)

- Define design tokens as centralized CSS variables (colors, typography, spacing).
- Reuse shared partials/components for recurring UI patterns (status badge, alerts, pagination, form field blocks).
- Keep page-specific overrides minimal and avoid hardcoded ad hoc colors.
- Ensure all new pages pass responsive checks at all defined breakpoints.

---

## Definition of Done (Frontend)

A frontend deliverable is complete only if:

1. It follows this color, typography, spacing, and component system.
2. It is fully responsive and functional at all breakpoints.
3. It includes proper UX feedback for loading/success/error/empty states.
4. It meets accessibility requirements (keyboard, focus, contrast, labels).
5. Motion/animation behavior follows this spec, including reduced-motion support.
6. It does not introduce inconsistent patterns against this spec.

---

## Change Governance

- Any deviation from this spec must be documented as an explicit design decision.
- Proposed UI changes that affect reusable patterns must update this specification first.
