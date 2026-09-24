# Implementation Plan: Dev Containers with DevPod

**Branch**: `devpod-implementation` | **Date**: 2026-09-24 | **Spec**: [spec_011-devpod.md](./spec_011-devpod.md)

---

## Summary

Add a standard `.devcontainer/` whose `workspace` container (the existing `development` image, .NET 10 SDK) joins the running Compose stack, so the editor gets a working .NET 10 toolchain. Launch it with DevPod (community fork, pinned) or any other dev-container tool. No application code, and no change to `compose.yaml`.

---

## Technical Context

**New tooling**: DevPod `skevetter/devpod` v0.26.1 (host CLI), `@devcontainers/cli` 0.89.0 (verification, via `npx`)
**Reused**: `Dockerfile` `development` stage; `web` service definition from `compose.yaml` via `extends`
**Constraints**: host SDK 7.0.304 cannot build `net10.0`; `docker compose up` unchanged (FR-006); no second stack (FR-004)

---

## Common Artifacts (DO NOT DUPLICATE)

- **Docker stack**: [`005-docker-containerization`](../005-docker-containerization/plan.md) — bin/obj isolation (R-005), root in container (R-007)
- **Architecture**: [`specs/common/architecture.md`](../common/architecture.md)

---

## Constitution Check

| Gate | Status |
|------|--------|
| §3.2 / §12.2 — new tooling needs an approved decision | ✅ ADR-004 |
| §7.2 — version pinning | ✅ DevPod v0.26.1 (SHA-256 verified), CLI 0.89.0; image is the existing pinned SDK stage |
| §7.2 — secrets outside the repository | ✅ Connection string inherited from `web`, password from git-ignored `.env` |
| §14 — ADR and documentation | ✅ ADR-004, this spec, README |
| §16.1 — Git workflow | ✅ Branch `devpod-implementation` |

---

## Project Structure

```text
.devcontainer/                         # NEW
├── devcontainer.json                  # standard keys only; service `workspace`
└── compose.devcontainer.yaml          # `workspace` extends `web`; external stack network
docs/adr/ADR-004-devpod-dev-containers.md   # NEW
README.md, docs/Keep-in-mind.md, .specify/specs/common/architecture.md   # MODIFIED
```

---

## Phase Breakdown

1. **Dev container definition** — `devcontainer.json`, `compose.devcontainer.yaml`. Exit: merged Compose config valid, plain config unchanged.
2. **Verify with the reference CLI** — `@devcontainers/cli up`, build, tests, `/health`. Exit: baseline reproduced.
3. **Verify with DevPod** — install pinned fork, `devpod up`, same checks. Exit: baseline reproduced, stack untouched.
4. **Documentation** — spec, ADR-004, README, Keep-in-mind, architecture.
