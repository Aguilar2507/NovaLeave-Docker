# ADR-003: AI Agent Teams with Docker Agent, Model Runner and Sandboxes

**Status**: Accepted
**Date**: 2026-09-23
**Deciders**: Product Owner, Development Team
**Constitution Reference**: v4.0.0 — §3.2 (Approved Stack), §7.2 (Secure Configuration), §7.3 (Sensitive Data), §12.2 (Infrastructure), §14 (ADRs)
**Specification**: [spec_010-docker-agents](../../.specify/specs/010-docker-agents/spec_010-docker-agents.md)
**Supersedes nothing. Builds on**: [ADR-001](./ADR-001-docker-local-development-environment.md), [ADR-002](./ADR-002-prometheus-observability.md)

---

## Context

NovaLeave's process is heavily documented — a constitution, Spec Kit specifications, ADRs, Clean Architecture layer rules — and following it takes effort on every change. Its local stack now also produces metrics (spec_007) and logs (spec_009) that are only useful if someone reads them.

AI agents can carry part of that load, but only under two conditions: they are held to the same rules as a developer, and they cannot damage the machine or leak its secrets. Docker ships three features aimed at exactly this:

- **Docker Agent** — multi-agent teams defined in YAML: a root agent delegating to sub-agents, each with its own model, instructions and tools.
- **Compose `models:` with Docker Model Runner** — an LLM declared as a Compose dependency and served locally.
- **Docker Sandboxes** — microVMs with their own filesystem, network and Docker daemon, sharing only the project folder.

None of these is in the Constitution's approved stack (§3.2), and §12.2 requires an approved decision before adding infrastructure. Hence this ADR.

## Decision

1. **Two teams, split by where they must run.** A *dev team* (planner, reviewer, tester) runs in a Docker Sandbox; an *ops team* (devenv, observability) runs as a Compose service on the stack's network. A sandbox cannot see the host's stack, so no single placement serves both.
2. **Restrictions live in configuration, not prompts**: filesystem allow-lists, `permissions.deny` for `src/`, read-only toolsets, enumerated script arguments, a fetch domain allow-list, and a fail-closed safety mode for the ops team.
3. **No agent writes production code.** The dev team plans, reviews and tests.
4. **Script tools instead of a shell** — `dotnet_build`, `dotnet_test`, `git_diff`, `compose_ps`, `compose_logs`, `container_health`.
5. **One named model per team, switched by a flavor**: Google Gemini `gemini-3.8-flash` by default, `ai/qwen3:8b-q4_K_M` through Model Runner with `--flavor local` (dev) or the `ops-agent-local` service (ops).
6. **Everything pinned**: `docker/docker-agent:1.140.0`, `ai/qwen3:8b-q4_K_M`, config schema `v1.140.0`.
7. **Opt-in**: the agent services sit behind the `agents` profile; `docker compose up` is unchanged.

> **Amendment (2026-09-23)**: the cloud provider was first drafted as Anthropic (`claude-sonnet-5`) and changed to Google Gemini at the Product Owner's choice. Only the `models.default` block, the key variable and the data-egress notes changed; Docker Agent's provider abstraction left every agent, tool and restriction untouched.

## Alternatives Considered

| Decision | Alternatives rejected | Reason |
|----------|----------------------|--------|
| Two teams by runtime location | **One team, sandboxed** | Ops agents would see an empty Docker daemon and no Prometheus or Loki |
| | **One team, unsandboxed** | Agents that write files and run builds would run with full host access |
| Script tools | **`shell` toolset with approval prompts** | Approval fatigue turns into "approve all"; the ops team would hold a shell alongside the Docker socket |
| Config-enforced containment | **Instructions only** | A prompt is a request, not a control. A model that ignores it must still be unable to write `src/` |
| Flavor switch | **Two YAML files per team** | Duplicated instructions drift apart |
| | **`--model` flag per run** | Must be repeated for every agent in the team |
| Separate `ops-agent-local` service | **One service, flavor passed as an argument** | Binding `models:` makes Compose require Model Runner even for cloud runs |
| Docker Agent | **Custom LangChain / SDK code** | Code to maintain in a .NET repository; no built-in sandbox, flavors or permission model |
| Qwen3 8B Q4_K_M | **Larger local models** | 18 GB+ does not fit next to the stack and an emulated SQL Server on a 16 GB machine |

## Consequences

### Positive

- The project's rules become executable: the reviewer checks layer dependencies against `architecture.md`, and the planner writes in the Spec Kit format against the constitution.
- The metrics and logs from spec_007/009 become answerable in plain language, with the query shown next to each answer.
- Builds and tests always use the .NET 10 SDK container — never the host's incompatible SDK.
- A fully local option exists: no key, no cost, no data leaving the machine.
- Zero changes to application code, and the default workflow is untouched.

### Negative

- **Isolation depends on using the launcher.** docker-agent 1.140 cannot honour `runtime.sandbox: true` (GAP-010-7), so `docker/agents/dev-team.sh` supplies `--sandbox`; running the YAML directly is unsandboxed.
- **The sandboxed dev team is cloud-only for now** — docker-agent mangles `--flavor` on the way into the sandbox (GAP-010-8).
- **The Gemini free tier rate-limits multi-agent use** within a few questions (GAP-010-9).
- Agent instructions embed facts that live elsewhere (metric names, test baselines) and must be kept in step (GAP-010-6).
- The local model is weak at multi-step delegation (GAP-010-4).
- Cloud use has a per-token cost billed to whoever supplies the key.

### Neutral

- Session history persists in a named volume and is discarded with `docker compose down -v`.

## Security Analysis

### Secrets

| Secret | Control |
|--------|---------|
| `GOOGLE_API_KEY` | Only in the git-ignored `.env` or the shell; `.env.example` ships it empty |
| SA password, connection string, Grafana password | Live in container environments. **A full `docker inspect` returns them.** `container_health` is limited to `.State` and `.RestartCount`; verified with zero hits for both passwords across `db`, `web` and `grafana` |
| Anything a tool happens to return | docker-agent's `redact_secrets` is on by default and scrubs known token formats before they reach the model |

### Data leaving the machine

- **With the cloud model, everything the agents read goes to Google**: code, specs, logs, metrics. **On the Gemini API's free tier, Google may also use that content to improve its products**; a billed key avoids that. Acceptable for this repository and its seeded data. **The ops team must never be pointed at real data with the cloud model** (GAP-010-3, Constitution §7.3).
- **docker-agent telemetry** is on by default, and its command events include positional arguments — which can be the prompt itself. Found during verification. Disabled in the Compose services (`TELEMETRY_ENABLED=false`) and documented for host runs.

### The Docker socket, again

`ops-agent` mounts the Docker socket, the same exposure accepted for Alloy in spec_009 (GAP-009-1): effectively root on the Docker daemon. What bounds it here is that **the agent has no shell** — three fixed commands with enumerated arguments, under a safety mode that denies anything not explicitly allowed. A vulnerability in the agent runtime itself would still inherit the socket's access. Development only (GAP-010-2).

### Network

- Dev team: the sandbox proxy is default-deny; only `mcr.microsoft.com`, `*.data.mcr.microsoft.com` and `api.nuget.org` are added.
- Ops team: `fetch` is limited to the `prometheus` and `loki` hosts. `allow_private_ips` is required because Docker-network addresses are private, and is safe only in combination with that domain allow-list.

## Compliance

| Rule | Status |
|------|--------|
| §3.2 / §12.2 — infrastructure needs an approved decision | ✅ This ADR |
| §7.2 — explicit version pinning | ✅ Image, model and schema pinned |
| §7.2 — secrets outside the repository | ✅ Verified; container environments not exposed to tools |
| §7.3 — sensitive data | ⚠️ Acceptable for seeded data only; cloud model must not see real data (GAP-010-3) |
| §2.I — Clean Architecture | ✅ No application code changed |
| §14 — ADR and documentation | ✅ This ADR + spec_010 |
| §16.1 — Git workflow | ✅ Branch `docker-agents` |

### Deviations

None.

### Gaps Carried Forward

| ID | Gap |
|----|-----|
| GAP-010-1 | *Resolved* — `sbx` installed, policy initialised, Google key stored as an `sbx` service secret |
| GAP-010-2 | `ops-agent` mounts the Docker socket. Development only |
| GAP-010-3 | Cloud model receives everything agents read. Seeded data only |
| GAP-010-4 | Local 8B model weak at delegation and tool calling |
| GAP-010-5 | No automated agent evaluations (`docker agent eval`) |
| GAP-010-6 | Prompts duplicate facts from the metrics catalogue and test baselines |
| GAP-010-7 | Sandboxing by launcher script, not `runtime.sandbox` — docker-agent 1.140 bug. Deviation from FR-002 |
| GAP-010-8 | Local model unavailable to the sandboxed dev team — `--flavor` mangled by docker-agent 1.140; launcher refuses it |
| GAP-010-9 | Gemini free-tier quota too low for regular multi-agent use |

## Verification

Executed 2026-09-23 on Docker 29.8.0 / Compose v5.5.1 / Docker Agent v1.140.0, Apple M2 Pro, 16 GB:

- Both teams validate; every intended tool resolves (`docker agent debug toolsets`), zero warnings.
- Every ops tool's exact command returned live data from inside the `ops-agent` container; secret audit clean.
- Prometheus and Loki reachable by service name from the agent's network.
- The agents' `dotnet_test` command reproduces the baseline: Domain 63/63, Application 49/49.
- The dev team fails closed without sandbox support.
- Live ops conversations on the **local model** answered a health question and a p95-latency question correctly, delegating to the right sub-agent and showing the command or PromQL used (22.96 ms reported vs 23.22 ms queried directly).
- On **Gemini**, the ops team answered the p95 question as 38.1 ms, identical to the direct query.
- **Sandboxed on Gemini with every call auto-approved (`--yolo`), the tester's attempt to edit `src/` was refused** by `permissions.deny`; the file was byte-for-byte unchanged.
- **Not yet verified**: a permitted sandbox write reaching the host, and `dotnet_test` inside the sandbox — blocked by the Gemini quota (GAP-010-9).

Full record in [tasks.md](../../.specify/specs/010-docker-agents/tasks.md).

## References

- [spec_010](../../.specify/specs/010-docker-agents/spec_010-docker-agents.md)
- Docker Agent: https://docs.docker.com/ai/docker-agent/
- Compose models: https://docs.docker.com/ai/compose/models-and-compose/
- Docker Sandboxes: https://docs.docker.com/ai/sandboxes/get-started/
