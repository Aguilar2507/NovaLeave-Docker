# Feature Specification: Dev Containers with DevPod

**Feature Branch**: `devpod-implementation`
**Created**: 2026-09-24
**Status**: Implemented — VS Code attach pending user confirmation (SC-006)
**Constitution Reference**: NovaLeave — Constitution v4.0.0, §3.2 (Approved Stack), §7.2 (Secure Configuration), §12.2 (Infrastructure), §14 (ADRs)
**Depends on**: `005-docker-containerization`
**Decision record**: [ADR-004](../../../docs/adr/ADR-004-devpod-dev-containers.md)

---

## Overview

The host's .NET SDK is 7.0.304; the solution targets `net10.0`. Everything that *runs* is containerized (spec_005), but the **editor** still runs on the host — so IntelliSense, the debugger and Test Explorer cannot work, and every build goes through a `docker run … sdk:10.0` command.

A **dev container** fixes that: the editor's language server, debugger and terminal run inside a container that has the .NET 10 SDK, while the window stays on the host. It is defined by `.devcontainer/devcontainer.json`, an open specification read by several tools:

| Tool | Status | Used here |
|---|---|---|
| **DevPod** (client-only, providers for local Docker, SSH, Kubernetes, cloud) | Upstream `loft-sh/devpod` unmaintained since 2025; community fork `skevetter/devpod` v0.26.1 | Documented launcher (PO choice) |
| **VS Code Dev Containers** | Microsoft, maintained | Works from the same file |
| **`@devcontainers/cli`** | Microsoft, maintained | Verification |

Because DevPod's future is uncertain (ADR-004), **only standard `devcontainer.json` keys are used**. Nothing here depends on DevPod continuing to exist.

**Design**: a separate **`workspace`** container, built from the existing `development` stage, joins the running stack's network. `web` keeps serving the app under `dotnet watch` on 8080, unchanged. You edit, build, debug and test in `workspace`, and can run a second copy of the app there on 5080.

**Out of scope**: JetBrains/Rider configuration, remote providers (SSH/cloud — documented, not configured), Codespaces, and giving the workspace Docker access.

---

## Requirements

- **FR-001**: The dev container MUST be defined only with keys from the open Dev Container specification.
- **FR-002**: The container MUST provide the .NET 10 SDK used by the rest of the project (the `development` Dockerfile stage).
- **FR-003**: It MUST reach the stack's `db` by service name, with the same connection string as `web`.
- **FR-004**: Opening or closing a dev container MUST NOT start a second copy of the stack, restart it, or stop it.
- **FR-005**: `web` MUST keep running `dotnet watch` on host port 8080, unchanged.
- **FR-006**: A plain `docker compose up` MUST be unchanged — the dev container files are read only by dev-container tools.
- **FR-007**: The container's build output MUST NOT mix with the host's or `web`'s (bin/obj isolation, spec_005 R-005), including test projects.
- **FR-008**: The workspace MUST NOT mount the Docker socket.
- **FR-009**: VS Code MUST get the C# tooling installed automatically.
- **FR-010**: DevPod MUST be pinned: community fork `skevetter/devpod` **v0.26.1**, binary verified against its published SHA-256.

## Success Criteria

- **SC-001**: `docker compose config --services` unchanged (6 services). ✅
- **SC-002**: `@devcontainers/cli` 0.89.0 `up` succeeds; inside: SDK 10.0.401, build clean, Domain 63/63, Application 49/49, Presentation 25 failures (24–26 baseline). ✅
- **SC-003**: DevPod v0.26.1 `up` succeeds; inside: SDK 10.0.401, 0 warnings / 0 errors, same test results. ✅
- **SC-004**: From the workspace, `db` resolves and the app started on 5080 answers `/health` → `Healthy`; `web` on 8080 still `Healthy`. ✅ (both tools)
- **SC-005**: No second stack and no restart: the six `novaleave` containers kept their uptime (26 h) through every `up`; no new host files besides `.devcontainer/`. ✅
- **SC-006**: VS Code attaches with C# Dev Kit, IntelliSense resolves .NET 10 APIs, a breakpoint hits. ⏳ **User to confirm** — not observable from the CLI.

---

## How to use it

```bash
# once
gh release download v0.26.1 --repo skevetter/devpod --pattern devpod-darwin-arm64   # or the .dmg app
shasum -a 256 devpod-darwin-arm64   # expect 5a6b6564…c123a72d
install -m 755 devpod-darwin-arm64 /opt/homebrew/bin/devpod
devpod provider add docker

# macOS + Docker Desktop: DevPod needs the Docker socket (GAP-011-2). Either enable
#   Docker Desktop → Settings → Advanced → "Allow the default Docker socket to be used"
# or export this in your shell profile:
export DOCKER_HOST=unix://$HOME/.docker/run/docker.sock

# every day
devpod up . --ide vscode         # starts the stack if needed, then the workspace, then VS Code
devpod stop .                    # stops the workspace only; the stack keeps running
```

Without DevPod: VS Code → *Dev Containers: Reopen in Container*, or `npx @devcontainers/cli up --workspace-folder .`.

Inside the workspace:

```bash
dotnet build NovaLeave.slnx
dotnet test tests/NovaLeave.Domain.Tests
dotnet run --project src/NovaLeave.Presentation.Web --no-launch-profile   # → http://localhost:5080
```

---

## Key decisions

### Standard first, DevPod as the launcher

`loft-sh/devpod` has had no release since June 2025; a maintainer-side issue in July 2026 states it "has not been maintained for over a year". The most active fork has one maintainer, now building a successor (Devsy, beta). The durable asset is `devcontainer.json`, which VS Code, Codespaces and Microsoft's CLI also read; DevPod is one way to launch it.

### A separate workspace, not the `web` container

Attaching the editor to `web` would replace `dotnet watch` with debug tasks and change how the app runs for everyone. A separate container leaves `web` exactly as it is and lets both run side by side (8080 and 5080).

### The workspace joins the stack; it does not start it

The obvious configuration — let the dev-container tool start the whole stack — **does not survive DevPod**. DevPod ignores `name: novaleave` and names the Compose project itself; with the stack running, it either reused the running project's file list (so `workspace` never existed) or, with a different name, would have started a second stack whose published ports collide. So the stack is started on the host by `initializeCommand`, and the workspace runs with `runServices: ["workspace"]`, `depends_on` reset, on the external `novaleave_default` network. Both tools now behave identically.

---

## Recorded Gaps

- **GAP-011-1**: **DevPod's long-term maintenance is uncertain** (see above). Mitigated by FR-001: if DevPod stops working, VS Code Dev Containers or the devcontainer CLI use the same file unchanged. Devsy may become the successor.
- **GAP-011-2**: **On macOS Docker Desktop, DevPod needs `DOCKER_HOST` or the default socket.** DevPod replaces `DOCKER_CONFIG` with a temporary directory for credential forwarding, which hides the Docker Desktop context; Docker then falls back to `/var/run/docker.sock`, which Docker Desktop does not create by default. The provider option `DOCKER_HOST` did not reach that code path; the environment variable does. `initializeCommand` runs `env -u DOCKER_CONFIG` for the same reason (otherwise `docker compose` is not found: *"unknown shorthand flag: 'd'"*).
- **GAP-011-3**: The workspace requires the stack's network; `initializeCommand` guarantees it, but if the stack is later removed with `docker compose down`, the workspace loses `db` until the stack is up again.
- **GAP-011-4**: SQL Server still runs emulated on Apple Silicon. DevPod's SSH or cloud providers could run the same dev container on native amd64 — documented as an option, not configured.
- **GAP-011-5**: E2E tests (Playwright) need browsers not installed in the image; not addressed.
