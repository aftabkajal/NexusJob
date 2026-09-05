# NexusJob

A modular-monolith job board whose real subject is the build process: enforced
module boundaries, proven and continuously checked by CI from the first commit.
Story 1.1 is the walking skeleton — the .NET 10 Host plus three wiring-only
bounded-context modules, a Vite/React 19 SPA with Feature-Sliced Design layers,
build-breaking boundary gates on both sides, a `/health` operability floor, and a
same-origin container image.

## Run

From a fresh checkout, `docker compose up --build` starts the app plus PostgreSQL 18
in one stack. The Host serves the SPA and `/api/*` + `/health` from
<http://localhost:8080> (same origin, no CORS). Check `GET http://localhost:8080/health` —
`200 {"status":"healthy","database":"ok"}` once Postgres is ready, `503` otherwise.
The database connection string is supplied only via the `ConnectionStrings__Postgres`
environment variable; nothing secret is committed.

## Test

`dotnet test backend/NexusJob.sln` runs the ArchUnitNET boundary suite (the SM-2
gate). It fails the build if a module implementation references another module's
non-Contracts assembly, a `.Contracts` project references an implementation, any
project other than `NexusJob.Host` references an implementation, or a raw-SQL call
in a module names another module's schema.

## Lint

`cd frontend && npm install && npm run lint` runs `eslint-plugin-boundaries`, which
encodes the Feature-Sliced Design layer order `app -> pages -> widgets -> features
-> entities -> shared` as downward-only imports at severity `error`; `npm run build`
type-checks and produces `dist/`.
