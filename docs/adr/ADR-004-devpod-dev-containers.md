# ADR-004: Dev Containers, Launched with DevPod

**Status**: Accepted
**Date**: 2026-09-24
**Deciders**: Product Owner, Development Team
**Constitution Reference**: v4.0.0 — §3.2 (Approved Stack), §7.2 (Secure Configuration), §12.2 (Infrastructure), §14 (ADRs)
**Specification**: [spec_011-devpod](../../.specify/specs/011-devpod/spec_011-devpod.md)
**Supersedes nothing. Builds on**: [ADR-001](./ADR-001-docker-local-development-environment.md)

---

## Context

ADR-001 containerized everything that runs — but not the editor. The host SDK is 7.0.304 and the solution targets `net10.0`, so on the host the C# language server, debugger and test explorer cannot load the solution. Builds work only through `docker run … sdk:10.0`.

Dev containers solve this: a `.devcontainer/devcontainer.json` (an open specification) describes a container the editor attaches to, with its language server and debugger running inside. The Product Owner asked for **DevPod** — a client-only tool that launches dev containers on local Docker, SSH hosts, Kubernetes or cloud VMs, for VS Code or JetBrains.

Researching DevPod showed its upstream is unmaintained: `loft-sh/devpod`'s last stable release is v0.6.15 (March 2025), its last commit November 2025, and a July 2026 issue states it "has not been maintained for over a year". A community fork, `skevetter/devpod`, carries fixes (v0.26.1, June 2026) with a single maintainer who is now building a successor, Devsy (beta).

§3.2 does not list dev-container tooling, and §12.2 requires an approved decision for new infrastructure. Hence this ADR.

## Decision

1. **Standard first.** `.devcontainer/` uses only keys from the Dev Container specification. DevPod is the documented launcher; VS Code Dev Containers and `@devcontainers/cli` work from the same files.
2. **DevPod = community fork `skevetter/devpod` v0.26.1**, pinned and SHA-256 verified.
3. **A separate `workspace` container** extends `web` (same image, mounts and connection string) and replaces its command with `sleep infinity`. `web` keeps running `dotnet watch` on 8080; the workspace can run the app on 5080.
4. **The workspace joins the stack; it never starts it.** `initializeCommand` starts the stack on the host; the workspace runs alone (`runServices`, `depends_on` reset) on the external `novaleave_default` network.
5. **No Docker socket** in the workspace.
6. **Opt-in**: `compose.yaml` is unchanged; only dev-container tools read `.devcontainer/`.

## Alternatives Considered

| Decision | Alternatives rejected | Reason |
|----------|----------------------|--------|
| Standard `devcontainer.json`, DevPod as launcher | **DevPod-specific configuration** | Ties the project to a tool whose upstream is unmaintained |
| Fork v0.26.1 | **Upstream v0.6.15** | 18 months of fixes behind, frozen |
| | **Devsy** | Beta; successor not yet established |
| Separate workspace | **Attach to `web`** | Replaces `dotnet watch` for everyone; changes how the app runs |
| Workspace joins the stack | **Dev-container tool starts the whole stack** | **Proven not to work with DevPod — see Consequences** |
| No socket | **docker-outside-of-docker** | Root on the Docker daemon from inside the editor's container; not needed for building or testing |

## Consequences

### Positive

- The editor gets the .NET 10 SDK: IntelliSense, debugging and Test Explorer become possible for the first time on this machine.
- Onboarding is one command; the environment is code-reviewed like everything else.
- The same files work with four tools, so DevPod's fate does not strand the project.
- `web`, `compose.yaml` and the `docker compose up` workflow are untouched.
- The design leaves room for DevPod's SSH or cloud providers to run the environment on native amd64, escaping SQL Server emulation (not configured).

### Negative

- **DevPod on macOS Docker Desktop needs `DOCKER_HOST`** (or Docker Desktop's "Allow the default Docker socket" setting) — see below.
- The workspace depends on the stack's network existing; `docker compose down` removes it.
- A second running copy of the app (5080) is possible and could confuse; it is labelled in the port list.

### What verification found

The first design — the dev-container tool starts the whole stack in project `novaleave` — worked with Microsoft's CLI, which honours `name: novaleave`. **DevPod does not**: it names Compose projects itself. Forced to `novaleave`, it detected the already-running project, used that project's file list (without the override), never created the workspace and failed injecting its agent. Left to its own name, it would have started a second stack whose published ports (8080, 1433, 3000, 9090, 3100) collide with the first. The design was changed so the workspace is its own project on the stack's network; both tools then behave identically.

DevPod also replaces `DOCKER_CONFIG` with a temporary directory for credential forwarding. That hides the Docker Desktop context and CLI plugins, so Docker falls back to `/var/run/docker.sock` (absent by default on Docker Desktop) and `docker compose` is not found. The provider option `DOCKER_HOST` did not reach the failing code path; the environment variable does, and `initializeCommand` clears `DOCKER_CONFIG` for its own call.

## Security

- No Docker socket in the workspace; the container runs as root inside, matching `web` (spec_005 R-007).
- The connection string and SA password are inherited from `web`, read from the git-ignored `.env`.
- DevPod binary pinned by version and verified by SHA-256 against the release digest.
- DevPod runs an SSH server and agent inside the workspace for its tunnel; it is not published to the network.

## Compliance

| Rule | Status |
|------|--------|
| §3.2 / §12.2 — infrastructure needs an approved decision | ✅ This ADR |
| §7.2 — explicit version pinning | ✅ DevPod v0.26.1 + SHA-256; devcontainer CLI 0.89.0 |
| §7.2 — secrets outside the repository | ✅ Inherited from `.env` |
| §14 — ADR and documentation | ✅ This ADR + spec_011 |
| §16.1 — Git workflow | ✅ Branch `devpod-implementation` |

### Gaps Carried Forward

| ID | Gap |
|----|-----|
| GAP-011-1 | DevPod maintenance uncertain; mitigated by standard-only configuration |
| GAP-011-2 | macOS Docker Desktop: DevPod needs `DOCKER_HOST` or the default socket |
| GAP-011-3 | Workspace needs the stack's network |
| GAP-011-4 | SQL Server still emulated on Apple Silicon; remote providers not configured |
| GAP-011-5 | E2E (Playwright) browsers not installed |

## Verification

Executed 2026-09-24, Docker 29.8.0 / Compose v5.5.1, Apple M2 Pro:

- `@devcontainers/cli` 0.89.0 and DevPod v0.26.1 both bring up the workspace; SDK 10.0.401; build 0 warnings / 0 errors; Domain 63/63, Application 49/49, Presentation at its 24–26-failure baseline.
- From the workspace, `db` resolves and the app on 5080 reports `Healthy`; `web` on 8080 stays `Healthy`.
- The six stack containers kept their uptime through every run; no second stack; no new files on the host besides `.devcontainer/`.
- **Pending**: VS Code attach with IntelliSense and a breakpoint (user).

Full record in [tasks.md](../../.specify/specs/011-devpod/tasks.md).

## References

- [spec_011](../../.specify/specs/011-devpod/spec_011-devpod.md)
- DevPod: https://devpod.sh — fork https://github.com/skevetter/devpod — upstream status https://github.com/loft-sh/devpod/issues/1992
- Dev Container specification: https://containers.dev
