# Feature Specification: Docker Agents

**Feature Branch**: `docker-agents`
**Created**: 2026-09-23
**Status**: Implemented — end-to-end model runs pending (see Success Criteria)
**Constitution Reference**: NovaLeave — Constitution v4.0.0, §7.2 (Secure Configuration), §7.3 (Sensitive Data), §12.2 (Infrastructure), §14 (ADRs)
**Depends on**: `005-docker-containerization`, `007-prometheus-observability`, `009-log-aggregation`
**Decision record**: [ADR-003](../../../docs/adr/ADR-003-docker-agents.md)

---

## Overview

NovaLeave's development work follows a strict, documented process — Spec Kit specs, a constitution, ADRs, Clean Architecture layer rules — and its local stack now produces metrics and logs that someone has to read. Both are good candidates for AI agents, **provided the agents are held to the same rules as a developer** and cannot damage the machine they run on.

This feature adds two agent teams built with three Docker features that are designed to work together:

| Docker feature | What it provides here |
|---|---|
| **Docker Agent** (`docker agent`, v1.140.0) | The agents themselves: a root agent that delegates to specialist sub-agents, each with its own model, instructions and tools, defined declaratively in YAML |
| **Compose `models:`** + **Docker Model Runner** | A local LLM declared as a Compose dependency, like an image — pulled and served on the machine, endpoint injected into the service |
| **Docker Sandboxes** | A microVM with its own filesystem, network and Docker daemon, sharing only the project folder with the host |

The two teams are split by **where they must run**, because a sandbox cannot see the host's Compose stack:

| Team | File | Agents | Runs in | Can change things? |
|---|---|---|---|---|
| **dev** | `docker/agents/dev-team.yaml` | root → planner, reviewer, tester | Docker Sandbox (default) | Yes: specs/docs (planner) and tests (tester) only |
| **ops** | `docker/agents/ops-team.yaml` | root → devenv, observability | Compose service on the stack's network | **No** — every tool is read-only |

Each team runs on a **cloud model** (Google Gemini, `gemini-3.8-flash`) or a **local model** (`ai/qwen3:8b-q4_K_M` through Docker Model Runner), switched per run.

**Out of scope**: agents that modify production code (`src/`), agents acting on a deployed environment, CI integration, publishing the teams to a registry (`docker agent share push`), and automated agent evaluations (GAP-010-5).

---

## Requirements

- **FR-001**: Agent teams MUST be defined declaratively in the repository and reviewed like code. The runtime image MUST be pinned.
- **FR-002**: Agents that write files or run builds MUST run isolated from the host by default.
- **FR-003**: Each team MUST switch between the cloud and the local model with a single run-time choice, without editing files.
- **FR-004**: The local model MUST be declared in `compose.yaml` with a pinned tag.
- **FR-005**: The dev team's sandbox network MUST be default-deny, opening only the hosts the tools need.
- **FR-006**: Only `mcr.microsoft.com` (SDK image) and `api.nuget.org` (restore) MAY be opened beyond the model provider.
- **FR-007**: Write access MUST be enforced by tool configuration, not by prompts: the planner writes only under `.specify/` and `docs/`; the tester only under `tests/`; **no agent writes `src/`**.
- **FR-008**: Builds and tests MUST run in the `mcr.microsoft.com/dotnet/sdk:10.0` container — the host SDK cannot build `net10.0`.
- **FR-009**: The ops team MUST be read-only: no shell, fixed commands with enumerated arguments, and a fail-closed safety mode that denies any tool not explicitly allowed.
- **FR-010**: Secrets MUST NOT reach a model. Container environments (SA password, connection string, Grafana password) MUST NOT be returned by any tool; the API key (`GOOGLE_API_KEY`) lives only in the git-ignored `.env` or the shell.
- **FR-011**: The existing `docker compose up` / `down -v` workflow MUST be unchanged — agents are opt-in.
- **FR-012**: Starting the ops agent MUST NOT start the stack.
- **FR-013**: The observability agent MUST reach only Prometheus and Loki.
- **FR-014**: Docker Agent's usage telemetry MUST be disabled wherever the project controls the environment — its command events carry the prompt text (Docker Agent telemetry documentation, *Sensitive Data*).

## Success Criteria

- **SC-001**: Both teams validate and expose the intended tools, in both flavors. ✅ `docker agent debug toolsets` — root 6, planner 10, reviewer 14, tester 12; ops root 6, devenv 4, observability 2. Zero warnings
- **SC-002**: `docker compose up` starts exactly the six pre-existing services. ✅ `docker compose config --services` unchanged; agent services appear only with `--profile agents`
- **SC-003**: Every devenv tool returns correct data from the live stack, run inside the `ops-agent` container. ✅ `compose_ps`, `container_health`, `compose_logs` verified
- **SC-004**: `container_health` output contains no secret. ✅ Checked for `db`, `web`, `grafana` against the SA and Grafana passwords: zero hits
- **SC-005**: Prometheus and Loki answer from the ops-agent network by service name. ✅ `up` returned both targets at 1; Loki returned its labels
- **SC-006**: The build/test tool command reproduces the known baseline. ✅ Domain 63/63, Application 49/49, run through the exact `dotnet_test` command
- **SC-007**: A cloud and a local conversation each answer a stack question with evidence. ✅ **Local**: health and p95 questions answered correctly with the command/query shown (22.96 ms vs 23.22 ms queried directly). **Gemini**: p95 answered as 38.1 ms — identical to the direct query (`0.038125`)
- **SC-008**: The dev team runs sandboxed, and its write restrictions hold there. ◐ **Partly met** — sandboxed on Gemini under `--yolo`, the tester's `edit_file` on `src/` was refused (*"denied by permissions configuration"*), file checksum unchanged. **Pending**: a permitted write under `tests/` appearing in the host's `git status`, and `dotnet_test` inside the sandbox — both blocked by the Gemini free-tier quota (GAP-010-9)

---

## How to use it

```bash
# Dev team — only through the launcher (sandbox + pinned template + telemetry off)
sbx login && sbx policy init balanced                          # once
sbx secret set google --command "grep '^GOOGLE_API_KEY=' $PWD/.env | cut -d= -f2-"   # once
docker/agents/dev-team.sh                                      # Gemini; local model: GAP-010-8

# Ops team — against the running stack
docker compose up -d
docker compose run --rm ops-agent                              # cloud (key from .env)
docker compose run --rm ops-agent-local                        # local (Model Runner)
```

Example questions: *"Review my uncommitted changes and run the Application tests"* (dev), *"Write a spec for request auto-expiry"* (dev → planner), *"Is everything healthy?"* and *"What is the p95 latency over the last 15 minutes, and are there errors in the web logs?"* (ops).

---

## Key decisions

### Two teams, split by where they must run

A Docker Sandbox is a separate microVM with **its own Docker daemon and network**. An agent inside it can build and run containers safely, but it cannot see `prometheus`, `loki` or the stack's containers. Putting every role in one team would force a choice between isolation and usefulness. Splitting by runtime location keeps both: the team that writes and executes is isolated; the team that needs the stack gets no ability to write or execute arbitrary commands.

### Enforcement in configuration, not in prompts

Every restriction that matters is a tool setting, not an instruction: the filesystem `allow_list`, `permissions.deny` rules for `src/`, `readonly` toolsets, enumerated script arguments, the fetch `allowed_domains`, and the `restricted` safety mode. Instructions explain the rules to the model; the configuration enforces them. A model that ignores its instructions still cannot write `src/` or stop a container.

### One named model, patched by a flavor

Every agent references the model `default`. The `local` flavor — a JSON Merge Patch applied at run time — replaces it with the Model Runner definition. One switch moves the whole team, and nothing is duplicated.

### Two ops services, not one with a flag

Binding `models:` to a service makes Compose require Model Runner for that service. `ops-agent-local` extends `ops-agent` and adds the binding, so the cloud path never depends on Model Runner.

### Script tools, not a shell

The reviewer and tester get `dotnet_build` and `dotnet_test`, not a shell. Tests are chosen from an enumeration. The devenv agent gets three fixed Docker commands. This is what makes the ops team's Docker socket mount tolerable (GAP-010-2).

---

## Recorded Gaps

- **GAP-010-1** *(resolved)*: Sandbox support required `sbx` (installed, signed in, `sbx policy init balanced`) and a stored service secret (`sbx secret set google`). Inside the sandbox `GOOGLE_API_KEY` is a `proxy-…` placeholder and the proxy substitutes the real key; without the stored secret Google answers **HTTP 400**, which reads like a config error but is not.
- **GAP-010-2** *(security)*: **`ops-agent` mounts the Docker socket**, the same exposure as Alloy (GAP-009-1): effectively root on the Docker daemon. Bounded by having no shell and only three fixed read-only commands, but a vulnerability in the agent runtime itself would inherit that access. Development only.
- **GAP-010-3** *(data)*: **With the cloud model, everything the agents read is sent to Google** — source code, specs, container logs and metrics. On the Gemini API's free tier, Google may also use that content to improve its products; a billed key avoids that. Acceptable for this repository and its seeded development data. Constitution §7.3 classifies request reasons and personal identifiers as Sensitive; **the ops team must never be pointed at an environment holding real data** with the cloud model.
- **GAP-010-4**: The local 8B model is markedly weaker than the cloud model at multi-step delegation and tool calling. It is suitable for simple ops questions; expect the dev team to need the cloud model for reviews.
- **GAP-010-5**: No automated evaluation of agent behavior. `docker agent eval` exists and could pin expected answers to recorded sessions.
- **GAP-010-6**: Prompts embed facts that live elsewhere — metric names, test baselines, CLEF field mapping. When the metrics catalogue or the spec_006 baseline changes, the agent instructions must change with it.
- **GAP-010-7** *(deviation from FR-002)*: **Isolation is by launcher, not by default.** `runtime.sandbox: true` was planned, but docker-agent 1.140 re-reads it inside the sandbox and refuses to start (*"already running inside a Docker sandbox"*). The team is sandboxed through `docker/agents/dev-team.sh` (`--sandbox`, template pinned to `1.140.0` — `latest` is rebuilt from unreleased code). Running `docker agent run docker/agents/dev-team.yaml` directly is **not** sandboxed; FR-007's restrictions still apply. Restore `runtime.sandbox: true` when a fixed release ships.
- **GAP-010-8**: **The local model is unavailable to the sandboxed dev team.** docker-agent 1.140 forwards `--flavor local` into the sandbox as `--flavor [local]`; that flavor does not exist and unknown flavors are ignored, so the run **silently used Gemini**. The launcher refuses `--flavor` rather than send data to the cloud unexpectedly. Whether the sandbox could reach Model Runner at all is still unknown. The ops team's local path is unaffected.
- **GAP-010-9**: **Gemini free-tier quota.** One multi-agent question makes several model calls; the free tier answered `HTTP 429` after a handful of questions and kept doing so for several minutes. Adequate for occasional use; a billed key is needed for regular use (and also resolves the data-use concern in GAP-010-3).
