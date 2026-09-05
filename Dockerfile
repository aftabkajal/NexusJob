# syntax=docker/dockerfile:1

# ---- Stage 1: build the React SPA -------------------------------------------
FROM node:24-alpine AS frontend
WORKDIR /src/frontend

# Install against the lockfile first for a cacheable layer.
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ ./
RUN npm run build
# Output: /src/frontend/dist

# ---- Stage 2: publish the .NET Host ---------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src

# The whole backend tree (Central Package Management needs Directory.*.props).
COPY backend/ ./backend/
RUN dotnet restore backend/NexusJob.sln
RUN dotnet publish backend/NexusJob.Host/NexusJob.Host.csproj \
    -c Release -o /app/publish --no-restore

# Same-origin hosting (AD-12): the SPA build is served as static files by the Host.
COPY --from=frontend /src/frontend/dist /app/publish/wwwroot

# ---- Stage 3: runtime ----------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# libgssapi-krb5-2: Npgsql probes libgssapi at startup; without it the native
#   loader writes a non-JSON line to stderr. Installing it keeps every log line
#   structured JSON.
# curl: used by the docker-compose healthcheck to hit GET /health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=backend /app/publish ./

# Drop root (the aspnet image predefines the non-root APP_UID).
USER $APP_UID

# The aspnet image already listens on 8080 (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080
ENTRYPOINT ["dotnet", "NexusJob.Host.dll"]
