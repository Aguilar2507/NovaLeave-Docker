# syntax=docker/dockerfile:1
#
# NovaLeave application image (spec_005 T202 / FR-001, FR-014).
#
# Stages:
#   base        shared runtime layer
#   build       restore + compile
#   publish     framework-dependent publish output
#   development dotnet watch, used by compose.yaml -- the day-to-day dev loop
#   final       production-shaped runtime, non-root
#
# The development target is what compose.yaml builds today. `final` is kept correct and
# production-shaped so the deferred production specification inherits a working image
# rather than a rewrite (research.md R-001).

# ---- base -------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
# HTTP only. TLS terminates at a proxy in production (research.md R-006); with no HTTPS
# port configured, UseHttpsRedirection() logs a warning and passes the request through
# rather than looping, so the app stays reachable here.
EXPOSE 8080

# ---- build ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Manifests first, source second: this is the whole point of the stage split. Editing a
# .cs file must invalidate only the compile layer, never the NuGet restore layer.
COPY Directory.Build.props NovaLeave.slnx ./
COPY src/NovaLeave.Domain/NovaLeave.Domain.csproj src/NovaLeave.Domain/
COPY src/NovaLeave.Application/NovaLeave.Application.csproj src/NovaLeave.Application/
COPY src/NovaLeave.Infrastructure/NovaLeave.Infrastructure.csproj src/NovaLeave.Infrastructure/
COPY src/NovaLeave.Presentation.Web/NovaLeave.Presentation.Web.csproj src/NovaLeave.Presentation.Web/
COPY tests/NovaLeave.Domain.Tests/NovaLeave.Domain.Tests.csproj tests/NovaLeave.Domain.Tests/
COPY tests/NovaLeave.Application.Tests/NovaLeave.Application.Tests.csproj tests/NovaLeave.Application.Tests/
COPY tests/NovaLeave.Presentation.Tests/NovaLeave.Presentation.Tests.csproj tests/NovaLeave.Presentation.Tests/
COPY tests/NovaLeave.E2E.Tests/NovaLeave.E2E.Tests.csproj tests/NovaLeave.E2E.Tests/

RUN dotnet restore NovaLeave.slnx

COPY . .
RUN dotnet build src/NovaLeave.Presentation.Web/NovaLeave.Presentation.Web.csproj \
        -c Release --no-restore

# ---- publish ----------------------------------------------------------------
FROM build AS publish
RUN dotnet publish src/NovaLeave.Presentation.Web/NovaLeave.Presentation.Web.csproj \
        -c Release --no-restore --no-build -o /app/publish

# ---- development ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /src

ENV ASPNETCORE_HTTP_PORTS=8080;9464
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# macOS bind mounts do not deliver inotify events into the container, so the file
# watcher must poll or hot reload silently never triggers (FR-015).
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

# Restart on edits hot reload cannot apply, instead of waiting at an interactive
# prompt no one is watching in `compose up` output.
ENV DOTNET_WATCH_RESTART_ON_RUDE_EDIT=1

# There is no browser inside the container: without this, every start logs a
# "Failed to launch ... No such file or directory" error that looks like a fault.
ENV DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER=1

EXPOSE 8080

# Restore during build so the first `compose up` is not a cold NuGet restore. Source
# arrives via bind mount at runtime, so no COPY of source belongs in this stage.
COPY Directory.Build.props NovaLeave.slnx ./
COPY src/NovaLeave.Domain/NovaLeave.Domain.csproj src/NovaLeave.Domain/
COPY src/NovaLeave.Application/NovaLeave.Application.csproj src/NovaLeave.Application/
COPY src/NovaLeave.Infrastructure/NovaLeave.Infrastructure.csproj src/NovaLeave.Infrastructure/
COPY src/NovaLeave.Presentation.Web/NovaLeave.Presentation.Web.csproj src/NovaLeave.Presentation.Web/
COPY tests/NovaLeave.Domain.Tests/NovaLeave.Domain.Tests.csproj tests/NovaLeave.Domain.Tests/
COPY tests/NovaLeave.Application.Tests/NovaLeave.Application.Tests.csproj tests/NovaLeave.Application.Tests/
COPY tests/NovaLeave.Presentation.Tests/NovaLeave.Presentation.Tests.csproj tests/NovaLeave.Presentation.Tests/
COPY tests/NovaLeave.E2E.Tests/NovaLeave.E2E.Tests.csproj tests/NovaLeave.E2E.Tests/
RUN dotnet restore NovaLeave.slnx

# Runs as root deliberately: dotnet watch writes into the bind-mounted source tree, and
# reconciling host UIDs across macOS and Linux costs more than it buys for a local
# development container (research.md R-007).
# --no-launch-profile is required, not cosmetic. Properties/launchSettings.json is a
# developer-machine artifact whose "https" profile binds https://localhost:7121; inside the
# container that fails hard with "No server certificate was specified" and the app never starts.
# Ignoring launch profiles lets ASPNETCORE_HTTP_PORTS (set by compose.yaml) bind both listeners:
# 8080 for the app and 9464 for /metrics only.
#
# --urls is deliberately NOT used: it overrides ASPNETCORE_HTTP_PORTS, which would collapse the
# two listeners back into one and re-expose metrics on the published port (spec_007 T401).
CMD ["dotnet", "watch", "--project", "src/NovaLeave.Presentation.Web/NovaLeave.Presentation.Web.csproj", \
     "run", "--no-launch-profile"]

# ---- final ------------------------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# The .NET images ship a non-root user, so there is no reason to run as root.
USER $APP_UID

ENTRYPOINT ["dotnet", "NovaLeave.Presentation.Web.dll"]
