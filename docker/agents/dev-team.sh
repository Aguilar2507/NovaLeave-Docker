#!/usr/bin/env sh
# NovaLeave -- launch the development agent team in a Docker Sandbox (spec_010, ADR-003).
#
#   docker/agents/dev-team.sh                    # Gemini (key via `sbx secret set google`)
#   docker/agents/dev-team.sh --agent reviewer   # talk to one sub-agent directly
#
# Extra arguments are passed to `docker agent run` unchanged.
#
# Why a script instead of `runtime.sandbox: true` in dev-team.yaml: docker-agent 1.140 re-reads
# that setting INSIDE the sandbox and refuses to start ("already running inside a Docker
# sandbox"). The --sandbox flag is not re-applied, so it works (spec_010 tasks.md, T306).
set -eu

cd "$(dirname "$0")/../.."

# docker-agent 1.140 forwards --flavor into the sandbox as "[local]", a flavor that does not
# exist, and unknown flavors are ignored -- so the team would SILENTLY run on the cloud model and
# send what it reads to Google. Refuse instead (spec_010 GAP-010-8).
for arg in "$@"; do
  case "$arg" in
    --flavor|--flavor=*)
      echo "dev-team.sh: --flavor is not supported inside a sandbox with docker-agent 1.140;" >&2
      echo "it would silently fall back to the cloud model. See spec_010 GAP-010-8." >&2
      exit 2
      ;;
  esac
done

# docker-agent telemetry includes command-line arguments, which can be the prompt (FR-014).
export TELEMETRY_ENABLED=false

# The sandbox template is pinned to the same version as the CLI. `latest` is rebuilt from
# unreleased code and is not what was verified (Constitution §7.2).
exec docker agent run docker/agents/dev-team.yaml \
  --sandbox \
  --template docker/docker-agent-sbx-templates:1.140.0 \
  "$@"
