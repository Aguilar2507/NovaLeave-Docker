# Tasks: Dev Containers with DevPod

**Input**: `.specify/specs/011-devpod/plan.md`, `spec_011-devpod.md`
**Prerequisites**: spec ✓, plan ✓, `005-docker-containerization` delivered ✓
**Tests**: No application code changes. Verified by executed procedure with two independent dev-container tools.

---

## Phase 1: Dev Container Definition

- [x] T101 `.devcontainer/compose.devcontainer.yaml` — `workspace` extends `web`; `sleep infinity`; `ports: !reset []`; `ASPNETCORE_HTTP_PORTS=5080`; bin/obj masks for the four test projects
- [x] T102 `.devcontainer/devcontainer.json` — standard keys only; `service: workspace`, `workspaceFolder: /src`, `shutdownAction: none`, `forwardPorts: [5080]`, `postCreateCommand: dotnet restore`, C# Dev Kit
- [x] T103 Validate — merged config correct; `docker compose config --services` unchanged

**Finding — paths in a second Compose file resolve against the project directory.** `extends.file: ../compose.yaml` failed (*"open …/Desktop/compose.yaml"*): in a multi-file project every relative path resolves against the first file's directory, the repo root. Fixed to `compose.yaml`.

**Finding — `!reset` empties, `!override` replaces.** `networks: !reset [stack]` produced no network at all; `!override` is the tag that replaces a value.

---

## Phase 2: Reference CLI (`@devcontainers/cli` 0.89.0)

- [x] T201 `devcontainer up` — success in 12 s; the six stack containers kept their 25 h uptime
- [x] T202 Build and tests inside — see record

| Check | Result |
|-------|--------|
| `dotnet --version` | ✅ 10.0.401 |
| `dotnet build NovaLeave.slnx` | ✅ 0 errors |
| Domain / Application | ✅ 63/63, 49/49 |
| Presentation | ✅ 25 failed / 21 passed / 3 skipped — within the 24–26 baseline |
| App on 5080 `/health` | ✅ `Healthy` after ~9 s (reaches `db`) |
| `web` on host 8080 `/health` | ✅ `Healthy` |

Only log line matching "error/fail": the known development warning *"Failed to determine the https port for redirect"* (HTTP-only).

---

## Phase 3: DevPod (`skevetter/devpod` v0.26.1)

- [x] T301 Install — `devpod-darwin-arm64` downloaded with `gh`, SHA-256 `5a6b6564…c123a72d` matched the release digest, installed to `/opt/homebrew/bin/devpod`; `docker` provider already present
- [x] T302 `devpod up . --ide none` — success after the three fixes below
- [x] T303 Build, tests and health through `devpod ssh` — see record
- [x] T304 Teardown — `devpod delete`; no leftover container or network

| Check | Result |
|-------|--------|
| SDK / `db` resolution | ✅ 10.0.401 / `172.18.0.2 db` |
| `dotnet build` | ✅ 0 warnings, 0 errors |
| Domain / Application / Presentation | ✅ 63/63, 49/49, 25 failures (baseline) |
| App on 5080 `/health` | ✅ `Healthy` |
| `web` 8080 / stack | ✅ `Healthy`; only project `novaleave` running, 26 h uptime; workspace in its own project |
| Host tree | ✅ nothing new besides `.devcontainer/` |

### Findings — three DevPod behaviours, each looking like something else

1. **`failed to connect to the docker API at unix:///var/run/docker.sock`.** DevPod swaps `DOCKER_CONFIG` for a temporary directory (credential forwarding), which hides the Docker Desktop context and the CLI plugins. Setting the provider option `DOCKER_HOST` did not reach the build path; the environment variable `DOCKER_HOST=unix://$HOME/.docker/run/docker.sock` does (GAP-011-2).
2. **DevPod ignores `name: novaleave` and reuses running projects.** It named the project `default-no-…`; forced to `novaleave` via `COMPOSE_PROJECT_NAME`, it found the running project, used *its* file list (`compose.yaml` only), never created `workspace`, and then failed injecting its agent into a container that did not exist (*"inject agent: EOF"* after ~2 min of retries). Redesigned: the workspace is its own project (`runServices: ["workspace"]`, `depends_on: !reset {}`) on the external `novaleave_default` network; the stack is started by `initializeCommand`.
3. **`initializeCommand: docker compose up -d` → "unknown shorthand flag: 'd'".** Same `DOCKER_CONFIG` swap: the `compose` plugin is not found, so `-d` reaches `docker` itself. Fixed with `env -u DOCKER_CONFIG docker compose up -d`.

The reference CLI was re-run after the redesign: still success, `db` resolves, stack untouched.

---

## Phase 4: Documentation

- [x] T401 spec_011, plan, tasks
- [x] T402 ADR-004
- [x] T403 README "Dev container" section; Keep-in-mind 2f; architecture row
- [ ] T404 VS Code attach: C# Dev Kit loads, IntelliSense on .NET 10 APIs, a breakpoint hits — **user to confirm** (SC-006)
