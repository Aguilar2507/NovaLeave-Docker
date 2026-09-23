# Implementation Plan: Docker Agents

**Branch**: `docker-agents` | **Date**: 2026-09-23 | **Spec**: [spec_010-docker-agents.md](./spec_010-docker-agents.md)

---

## Summary

Add two Docker Agent teams to the repository. A **dev team** (planner, reviewer, tester) runs in a Docker Sandbox and works on specs and tests. An **ops team** (devenv, observability) runs as an opt-in Compose service and reads the running stack. Both switch between Google's Gemini cloud model and a local Docker Model Runner model, declared through Compose `models:`.

Development environment only. No application code changes.

---

## Technical Context

**New tooling**: Docker Agent `v1.140.0` (CLI on the host; `docker/docker-agent:1.140.0` image for the ops service)
**New infrastructure**: Compose `models:` entry `agent-llm` → `ai/qwen3:8b-q4_K_M` (Docker Model Runner); two profiled services
**Model providers**: `google` (`gemini-3.8-flash`), `dmr` (local)
**Config schema**: Docker Agent config version `16`
**Storage**: named volume `novaleave-agent-data` (ops sessions); `novaleave-agent-nuget` (NuGet cache for agent builds, in whichever daemon runs them)
**Constraints**: host SDK is 7.0.304 and cannot build `net10.0` (FR-008); no secrets to a model (FR-010); `docker compose up` unchanged (FR-011)

---

## Common Artifacts (DO NOT DUPLICATE)

- **Architecture**: [`specs/common/architecture.md`](../common/architecture.md) — layer rules the reviewer enforces
- **Security**: [`specs/common/security.md`](../common/security.md) — sensitive data classification
- **Metrics**: [`007-prometheus-observability/metrics-catalogue.md`](../007-prometheus-observability/metrics-catalogue.md) — source of the observability agent's metric names
- **Logs**: [`009-log-aggregation`](../009-log-aggregation/spec_009-log-aggregation.md) — labels and CLEF field mapping

---

## Constitution Check

| Gate | Status |
|------|--------|
| §3.2 / §12.2 — new tooling needs an approved decision | ✅ ADR-003 |
| §7.2 — version pinning | ✅ `docker/docker-agent:1.140.0`, `ai/qwen3:8b-q4_K_M`, schema pinned to `v1.140.0` |
| §7.2 — secrets outside the repository | ✅ `GOOGLE_API_KEY` only in git-ignored `.env` / shell; container environments never returned by tools |
| §7.3 — sensitive data | ⚠️ Cloud model receives what agents read; acceptable for seeded development data only (GAP-010-3) |
| §2.I — Clean Architecture | ✅ No application code touched; the reviewer enforces the layer rules |
| §14 — ADR and documentation | ✅ ADR-003, this spec, README |
| §16.1 — Git workflow | ✅ Branch `docker-agents` |

---

## Project Structure

```text
docker/agents/                     # NEW
├── dev-team.yaml                  # root → planner, reviewer, tester; sandbox network allowlist
├── dev-team.sh                    # launcher: --sandbox, pinned template, telemetry off
└── ops-team.yaml                  # root → devenv, observability; safety: restricted

compose.yaml                       # MODIFIED — ops-agent, ops-agent-local (profile "agents"),
                                   #            top-level models: agent-llm, novaleave-agent-data
.env.example                       # MODIFIED — GOOGLE_API_KEY (empty), telemetry and free-tier notes
docs/adr/ADR-003-docker-agents.md  # NEW
README.md                          # MODIFIED — AI agents section
.specify/specs/common/architecture.md  # MODIFIED — agent tooling row
```

---

## Phase Breakdown

### Phase 1 — Agent definitions
1. `dev-team.yaml`: one named model `default` + `local` flavor; sandbox network allowlist (launched sandboxed by `dev-team.sh`); filesystem `allow_list` per agent; `permissions.deny` for `src/`; script tools `git_diff`, `dotnet_build`, `dotnet_test`.
2. `ops-team.yaml`: same model pattern with env-injected `local` flavor; `restricted` safety with explicit allow list; script tools `compose_ps`, `compose_logs`, `container_health`; `fetch` limited to `prometheus`, `loki`.

**Exit criteria**: `docker agent debug toolsets` lists every intended tool for both teams with zero warnings.

### Phase 2 — Compose integration
3. `ops-agent` (cloud) and `ops-agent-local` (extends it, binds `models:`), profile `agents`, no `depends_on`.
4. Top-level `models:` with the pinned local model.

**Exit criteria**: plain `config --services` unchanged; tools return live data from inside the container.

### Phase 3 — Verification and documentation
5. Execute every script tool's exact command against the live stack; audit output for secrets.
6. Run the build/test command and compare with the baseline.
7. End-to-end conversations with each model; sandboxed dev-team run.
8. ADR-003, README, architecture.md.

**Exit criteria**: every success criterion met or recorded as outstanding.
