# Deploying Heardit

Heardit ships as a single container (ASP.NET on .NET 10) backed by PostgreSQL. The
database schema is applied automatically on startup via EF Core migrations, so a fresh
container needs no manual DB setup.

## Configuration

All configuration comes from environment variables — no code or committed config changes
are needed per environment. Double underscores (`__`) map to nested configuration keys.

| Variable | Required | Purpose |
| --- | --- | --- |
| `ConnectionStrings__HearditDbContextConnection` | yes | Npgsql connection string, e.g. `Host=db;Port=5432;Database=heardit;Username=heardit;Password=…` |
| `Spotify__ClientId` | yes | Spotify app client ID ([dashboard](https://developer.spotify.com/dashboard)) |
| `Spotify__ClientSecret` | yes | Spotify app client secret |
| `PathBase` | no | Sub-path the app is served under when it shares a domain with other apps, e.g. `/heardit`. Generated URLs, static assets, login redirects and cookies all pick it up. |
| `DataProtection__KeyPath` | no | Directory for the data-protection key ring. `appsettings.Production.json` sets `/data/keys`; mount a volume there. |
| `AllowedHosts` | no | Host header allow-list, e.g. `example.com;www.example.com`. Defaults to `*`. |
| `ASPNETCORE_ENVIRONMENT` | no | `Production` (the container default) enables HSTS, friendly error pages, quieter logs and the key path above. |

The app validates the Spotify options at startup and will refuse to boot if either is missing.
`GET /healthz` answers `Healthy` without a login and without needing a forwarded scheme, for
proxies and uptime checks.

## Run with Docker Compose (app + PostgreSQL)

```bash
cp .env.example .env      # then edit .env with real secrets
docker compose up --build
```

The app is served on `http://localhost:8080`. Compose starts a `postgres:17-alpine` service,
waits for it to pass a `pg_isready` healthcheck, then starts the app, which migrates the schema
and seeds a sample song. Data persists in the named volume `heardit-pgdata`, so
`docker compose down && docker compose up` keeps existing accounts and reviews (use
`docker compose down -v` to wipe the volume).

The compose Postgres service does **not** publish a host port — the app reaches it over the
internal network as `db`. This lets it coexist with a standalone dev Postgres container (e.g.
`heardit-pg`) already bound to host port `5432`. To inspect the compose DB from the host, run
`docker compose exec db psql -U "$POSTGRES_USER" -d heardit`.

## Behind a reverse proxy (TLS termination)

On a small host, run a reverse proxy such as Caddy or nginx in front of the container to
terminate HTTPS and forward plain HTTP to port `8080`. The app honors `X-Forwarded-For` and
`X-Forwarded-Proto`, so HTTPS redirection, HSTS, and secure cookies work correctly. This trust
is unconditional (known-proxy/network checks are cleared), which assumes the app is reachable
**only** through the proxy — do not expose port `8080` directly to the internet.

## Production: trevorhuval.com

The live deployment is one service in the
[trevorhuval-infra](https://github.com/TrevorHuval/trevorhuval-infra) compose stack, next to
Plannit and MusiQL, served at `https://trevorhuval.com/heardit`.

- **Image.** Every push to `master` runs the tests and publishes
  `ghcr.io/trevorhuval/heardit:latest` (and a `sha-…` tag) from `.github/workflows/ci.yml`.
  The Docker build context is the `Heardit/` folder.
- **Routing.** Caddy matches `/heardit` and `/heardit/*` and proxies to `heardit:8080` with the
  path intact; the app runs with `PathBase=/heardit`. Its cookies are named `Heardit.Auth` and
  `Heardit.Antiforgery` and scoped to that path, so they never collide with Plannit's on the
  same domain.
- **State.** `heardit-db` (Postgres 17) holds the data in the `heardit-pgdata` volume, and the
  data-protection keys live in `heardit-data`, so redeploys keep everyone logged in.
  `scripts/backup.sh` in the infra repo dumps the database alongside the others.
- **Secrets** come from the infra `.env`: `HEARDIT_POSTGRES_PASSWORD`,
  `HEARDIT_SPOTIFY_CLIENT_ID`, `HEARDIT_SPOTIFY_CLIENT_SECRET`.

To ship a change: push to `master`, wait for CI, then run `./scripts/deploy.sh` on the server
(or `./scripts/sync-to-server.sh` from a workstation).

To try the sub-path setup locally, `dotnet run --launch-profile http-pathbase` serves the app
at `http://localhost:5047/heardit/`.

## Running locally without Docker

`dotnet run` still works for development. It uses the connection string in
`appsettings.json` (localhost Postgres) and Spotify credentials from user-secrets:

```bash
dotnet user-secrets set "Spotify:ClientId" "…"
dotnet user-secrets set "Spotify:ClientSecret" "…"
dotnet run
```

## Notes

- **Environment:** the container defaults to the `Production` environment (HSTS + friendly
  error pages). Set `ASPNETCORE_ENVIRONMENT=Development` only for debugging.
- **Data Protection keys** go to `/data/keys` in the `Production` environment (see
  `appsettings.Production.json`). Mount a volume over `/data`, or every container replacement
  logs everyone out and invalidates in-flight antiforgery tokens. The `docker-compose.yml` in
  this folder is a local/all-in-one setup and does not mount one.
