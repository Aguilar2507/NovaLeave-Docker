# Tasks: Docker Agents

**Input**: `.specify/specs/010-docker-agents/plan.md`, `spec_010-docker-agents.md`
**Prerequisites**: spec ✓, plan ✓, `005`, `007`, `009` delivered ✓
**Tests**: No application code changes, so no new unit tests. Agent configuration and tools are verified by executed procedure.
**Organization**: Grouped by phase, each with an explicit exit criterion.

---

## Phase 1: Agent Definitions

- [x] T101 Create `docker/agents/dev-team.yaml` — root, planner, reviewer, tester; sandbox network allowlist (sandboxing itself moved to `dev-team.sh`, see T306); one named model `default` and a `local` flavor
- [x] T102 Create `docker/agents/ops-team.yaml` — root, devenv, observability; `safety: restricted`; explicit `permissions.allow`
- [x] T103 Enforce write containment in configuration (FR-007): planner `allow_list: [.specify, docs]`; reviewer `readonly: true`; tester `allow_list: [tests, src]` plus `permissions.deny` on `write_file`/`edit_file` under `src/`
- [x] T104 Add `git_diff` to the reviewer — the built-in `git` toolset has status, log, show and blame but **no diff**
- [x] T105 Verify both teams with `docker agent debug toolsets`

**Exit criteria**: every intended tool listed, zero warnings. ✅ **Met.**

### Phase 1 Verification Record (2026-09-23)

| Agent | Tools | Notes |
|-------|-------|-------|
| dev root | 6 | think, 4 todo tools, `transfer_task` |
| planner | 10 | filesystem (read/write, contained) + think |
| reviewer | 14 | read-only filesystem, 5 git, think, `git_diff`, `dotnet_build`, `dotnet_test` |
| tester | 12 | filesystem + think + `dotnet_build`, `dotnet_test` |
| ops root | 6 | think, todo, `transfer_task` |
| devenv | 4 | think, `compose_ps`, `compose_logs`, `container_health` |
| observability | 2 | think, `fetch` |

**Finding — script tools silently disappear.** The first run listed no `dotnet_*` tools at all. The debug log showed `Toolset configuration failed; skipping ... uses undefined args: [PWD]`: docker-agent treats **every** `$NAME` and `${...}` in a script `cmd` as a tool argument, and drops the whole toolset — silently, outside `--debug` — if one is undeclared. `$PWD` became `$(pwd)`; `${base:-HEAD}` became a required `base` argument. Recorded in a comment in `dev-team.yaml` so the next edit does not reintroduce it. **Always check `debug toolsets` after editing a script tool.**

**Finding — `restricted` denies script tools.** Script tools carry no read-only annotation, so the fail-closed mode would deny all three devenv tools. They are allowed by name in `permissions.allow`; anything added later is denied until listed deliberately.

---

## Phase 2: Compose Integration

- [x] T201 Add `ops-agent` — `docker/docker-agent:1.140.0` (tag verified by pull; `v1.140.0` does not exist), profile `agents`, entrypoint carrying the config path so extra arguments append
- [x] T202 Add `ops-agent-local` — `extends: ops-agent`, binds `models: agent-llm` with `endpoint_var: AGENT_LLM_URL`, `model_var: AGENT_LOCAL_MODEL`, runs `--flavor local`
- [x] T203 Add top-level `models: agent-llm` → `ai/qwen3:8b-q4_K_M`, `context_size: 16384`
- [x] T204 Disable docker-agent telemetry in the service (FR-014)
- [x] T205 `.env.example` — `GOOGLE_API_KEY` empty, with data-egress, free-tier and telemetry notes

**Exit criteria**: default stack unchanged; tools return live data from inside the container. ✅ **Met.**

**Deviation from plan**: the plan proposed a custom `Dockerfile.ops`. Not needed — the official image already contains the Docker CLI.

### Phase 2 Verification Record (2026-09-23)

| Check | Result |
|-------|--------|
| `docker compose config --quiet` | ✅ Valid |
| `docker compose config --services` (no profile) | ✅ `db web loki alloy prometheus grafana` — unchanged (FR-011) |
| With `--profile agents` | ✅ Adds `ops-agent`, `ops-agent-local` |
| `ops-agent-local` inherits volumes, `user: root` | ✅ Confirmed in resolved config |
| `compose_ps` inside `ops-agent` | ✅ All six services listed with status and pinned image |
| `container_health db` | ✅ `"Health":{"Status":"healthy",...}`, restarts=0 |
| `compose_logs web 50` | ✅ 50 lines |
| Secrets in `container_health` output for `db`, `web`, `grafana` | ✅ **0** occurrences of the SA or Grafana password (FR-010) |
| `http://prometheus:9090/api/v1/query?query=up` from the container | ✅ Both targets `1` |
| `http://loki:3100/loki/api/v1/labels` from the container | ✅ `container, job, service, service_name` |

---

## Phase 3: Verification and Documentation

- [x] T301 Run the exact `dotnet_test` command for the baseline suites
- [x] T302 Dry-run the dev team to confirm sandbox behavior
- [x] T303 End-to-end conversation, cloud model (Gemini, ops team) — see record below
- [x] T304 End-to-end conversation, local model (ops team) — see record below
- [x] T305 Confirm `permissions.deny` refuses a tester write to `src/` — sandboxed, Gemini, `--yolo`: refused
- [x] T306 Sandboxed dev-team run — works through `docker/agents/dev-team.sh` (GAP-010-7)
- [ ] T309 Permitted `tests/` write visible on the host, and `dotnet_test` inside the sandbox — **blocked by Gemini free-tier quota** (GAP-010-9)
- [ ] T310 Local model for the sandboxed dev team — **blocked by docker-agent** (GAP-010-8)
- [x] T307 ADR-003
- [x] T308 README "AI agents" section; `architecture.md` row

### Phase 3 Verification Record (2026-09-23)

| Check | Result |
|-------|--------|
| `dotnet_test` → `NovaLeave.Domain.Tests` | ✅ 63/63 |
| `dotnet_test` → `NovaLeave.Application.Tests` | ✅ 49/49 |
| `docker agent run dev-team.yaml --dry-run` | ✅ **Fails closed**: *"--sandbox requires Docker Desktop with sandbox support"*, exit 1 — it does not fall back to running unisolated |
| `debug toolsets --flavor local` without the model pulled | ✅ Stops with pull instructions rather than a runtime failure |
| Both teams after the switch to Gemini (`provider: google`) | ✅ Same tool counts, zero warnings |
| `ops-agent-local`: *"Is everything healthy?"* | ✅ Root delegated to devenv, which called `compose_ps`, `container_health` and `compose_logs`; all seven containers listed correctly, `db` healthy with `ExitCode=0` probes. ~3 min |
| `ops-agent-local`: *"p95 latency over the last 5 minutes?"* | ✅ Root delegated to observability, which fetched Prometheus with the catalogue's p95 query and showed it: **22.96 ms**. Direct query 34 s later: **23.22 ms** — consistent |

| `ops-agent` (Gemini `gemini-3.8-flash`): *"p95 latency?"* | ✅ **38.1 ms** with query and raw result shown — identical to the direct query (`0.038125`). First attempt hit `HTTP 429`; succeeded after a 70 s wait |
| Sandboxed tester, Gemini, `--yolo`: *edit `src/NovaLeave.Domain/Entities/AccountStatus.cs`* | ✅ `read_file` allowed; `edit_file` → *"Tool 'edit_file' is denied by permissions configuration."* Checksum `31ce0ef9…` unchanged before and after |
| Sandboxed tester: write `tests/sandbox-probe.txt`; reviewer: `dotnet_test` | ⏳ `HTTP 429` on every attempt over ~10 min (GAP-010-9) |
| `dev-team.sh --flavor local` | ✅ Refused with an explanation, exit 2 (GAP-010-8) |

### Findings from the sandboxed runs

Five problems had to be solved before the dev team ran in a sandbox. Each is recorded because each presented as something else:

1. **`sbx policy init` is required first.** Without it: *"global network policy has not been initialized"*. Initialised to `balanced`.
2. **The sandbox needs a real terminal.** docker-agent runs `sbx exec -it`; without a TTY it exits in ~2 s with no output and no error. Interactive use is unaffected; scripted runs need `script -q`.
3. **`runtime.sandbox: true` breaks the sandboxed run** — re-read inside the VM: *"already running inside a Docker sandbox"*. Moved to the `--sandbox` flag in `dev-team.sh` (GAP-010-7).
4. **The default template floats.** `docker/docker-agent-sbx-templates:latest` is rebuilt from unreleased code (reports version `main`). Pinned to `1.140.0`, matching the CLI.
5. **HTTP 400 "request shape may be incompatible" was a missing credential.** Inside the sandbox the key is a `proxy-…` placeholder; the proxy substitutes the real key only if one is stored. Fixed with `sbx secret set google --command …`, which reads `.env` on the host so the key has one home.

Also observed: docker-agent adds `--yolo` itself for runs inside a sandbox, treating the microVM as the boundary. The `src/` denial above was obtained under `--yolo` — `permissions.deny` holds regardless.

**Local model (`ai/qwen3:8b-q4_K_M`) observations**: delegation and tool calling both worked on the first attempt; answers cited the query or command used, as instructed. It is slow (~3 min per question on an M2 Pro with the stack running) and prints its reasoning before the answer. Adequate for ops questions; untested for dev-team reviews (GAP-010-4).

**Finding — telemetry carries prompts.** The dry-run's debug log showed docker-agent posting a telemetry event whose `args` included the prompt text. Its documentation confirms positional arguments are collected and may contain sensitive data. This produced FR-014: `TELEMETRY_ENABLED=false` in the Compose service, and documented for host runs.
