# Quickstart: Running NovaLeave in Docker

**Feature Branch**: `docker-implementation`
**Date**: 2026-09-22
**Plan Reference**: [plan.md](./plan.md)
**Research Reference**: [research.md](./research.md)

Everything here was executed on an Apple M2 Pro before being written down. The commands are the ones that ran, not the ones that ought to work.

---

## Prerequisites

- **Docker Desktop** with Docker Engine 29.x and Compose v2 or later.
- **On Apple Silicon: Rosetta emulation must be enabled.** Docker Desktop → Settings → General → *Use Rosetta for x86/amd64 emulation*. SQL Server publishes no arm64 image, so without this the `db` container cannot start. Recent Docker Desktop versions enable it by default.
- At least **4 GB** allocated to Docker (SQL Server alone wants ~2 GB).
- **No .NET SDK required.** This is the point of the feature.

> **Note**: the host SDK on this project's machine is 7.0.304 while the solution targets `net10.0`, so the container is currently the only way to build NovaLeave. `dotnet build` on the host will fail regardless of Docker.

---

## Start the stack

```bash
cd NovaLeave-Docker
cp .env.example .env
docker compose up -d --build
```

The first run takes a few minutes: it pulls the .NET and SQL Server images and restores NuGet packages. Later starts take about 25 seconds.

Open **http://localhost:8080**.

### Sign in

All development accounts use the password `Test123!@#`.

| Email | Roles | Notes |
|-------|-------|-------|
| `user@novaleave.local` | User | Standard employee, 15 days balance |
| `approver@novaleave.local` | User + Approver | Can approve requests |
| `employee@novaleave.local` | User | Second employee |
| `manager@novaleave.local` | User + Approver | Second approver |
| `inactive@novaleave.local` | User | `Inactive` — used to test rejected sign-in |

These credentials are development-only and exist nowhere but a disposable local container.

---

## Everyday commands

```bash
docker compose up -d          # start
docker compose logs -f web    # follow application logs
docker compose ps             # status of both containers
docker compose down           # stop, KEEPING the database
docker compose down -v        # stop and DESTROY the database
docker compose up -d --build  # rebuild the image (after changing the Dockerfile or a NuGet package)
```

### Reset to a clean database

```bash
docker compose down -v && docker compose up -d
```

Removes the data volume; the next start re-applies migrations and re-seeds from scratch. Takes roughly 25 seconds.

---

## Editing code

The source directory is mounted into the `web` container and `dotnet watch` is running, so **you do not rebuild to see a change**:

- a `.cshtml` edit appears on the next request (~10 s);
- a `.cs` edit triggers an in-container rebuild and restart (~15 s), leaving the database untouched.

Rebuild the image (`up -d --build`) only when the `Dockerfile` changes or a NuGet package is added — a package change requires a fresh `restore`, which happens at image build time.

---

## Connecting a database client

Azure Data Studio, DataGrip, or `sqlcmd` can attach to the container:

| Setting | Value |
|---------|-------|
| Server | `localhost,1433` |
| User | `sa` |
| Password | the `MSSQL_SA_PASSWORD` from your `.env` |
| Database | `NovaLeave` |
| Encryption | Trust the server certificate |

Or query without leaving the terminal:

```bash
docker compose exec db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P 'NovaLeave!Dev2026' -C -d NovaLeave \
  -Q "SELECT Email, Balance FROM Employees"
```

---

## Verifying the stack is healthy

```bash
docker compose ps                        # both Up; db shows (healthy)
curl http://localhost:8080/health        # -> Healthy
```

`/health` is the strongest single check available: it runs a query through EF Core, so a `Healthy` response proves the web container is serving **and** that it can reach the database container. If that passes, everything below it works.

---

## Expected log output

A correct startup contains these lines:

```text
Applying pending migrations to server 'db,1433', database 'NovaLeave' (attempt 1/10).
Database migrations applied successfully.
Seeding development data.
✅ Development data seeded successfully
Now listening on: http://0.0.0.0:8080
Hosting environment: Development
```

Two messages are **normal and not faults**:

- `Failed to determine the https port for redirect` — the container serves plain HTTP by design; TLS terminates at a proxy in production. The middleware passes the request through rather than looping (research.md R-006).
- `No XML encryptor configured` — expected for local Data Protection keys.

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| `db` stays `(health: starting)` past ~2 minutes | Still initializing, or the SA password was refused | `docker compose logs db`. SQL Server requires 8+ characters using three of: uppercase, lowercase, digits, symbols — it exits on startup if the password fails that policy |
| `db` exits immediately on Apple Silicon | Rosetta emulation unavailable | Enable *Use Rosetta for x86/amd64 emulation* in Docker Desktop settings |
| `Bind for 0.0.0.0:8080 failed: port is already allocated` | Something else holds the port | Change `WEB_PORT` (or `DB_PORT`) in `.env`, then `docker compose up -d` |
| `web` restarts repeatedly | Application threw during startup | `docker compose logs web` — the initializer reports the exact server and database it could not reach |
| A new NuGet package is "not found" | Restore is baked into the image | `docker compose up -d --build` |
| Changes to a `.cs` file do nothing | File watcher not picking up the bind mount | Confirm `web` is running the `development` target; check `docker compose logs -f web` for a rebuild |
| `docker: command not found` | Docker's binary is not on `PATH` | It lives at `/Applications/Docker.app/Contents/Resources/bin/docker`; add that directory to `PATH` in `~/.zshrc`, or use Docker Desktop's terminal |
| Logged out after every rebuild | Data Protection keys not persisting | Should not occur — the `novaleave-dpkeys` volume exists for this. Verify it with `docker volume ls` |

---

## What the stack actually does

```text
docker compose up
        │
        ├─ db   SQL Server 2022 (CU27), linux/amd64 under Rosetta
        │       healthcheck: sqlcmd "SELECT 1" every 10s
        │       volume: novaleave-db-data → /var/opt/mssql
        │
        └─ web  waits for db to report HEALTHY, not merely started
                dotnet watch + bind-mounted source  → hot reload
                DatabaseInitializer → migrate, then seed
                serves http://localhost:8080
```

The readiness gate is the part worth understanding. SQL Server accepts TCP connections several seconds before it can answer a query, so waiting for the port would let the application start too early and fail intermittently on a cold machine. Compose waits for a query that actually returned a row.

---

## Known limitations

- **Development only.** There is no production image yet; `docker compose` here is not a deployment.
- **Migrations apply automatically**, which suits a single-replica development stack and nothing else. This is blocked in `Production` in code (GAP-005-3).
- **Do not benchmark against this stack.** SQL Server runs emulated on Apple Silicon and is measurably slower than native.
- **`NovaLeave.Presentation.Tests` fails 24–26 of 49 tests**, independently of Docker, for reasons documented in [spec 006](../006-integration-test-isolation/spec_006-integration-test-isolation.md).
