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

The app validates the Spotify options at startup and will refuse to boot if either is missing.

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
- **Data Protection keys** are stored on the container filesystem, so a container replacement
  invalidates existing auth/antiforgery cookies (users are logged out). For a single-instance
  cheap-box deployment this is acceptable; persist `/home/app/.aspnet/DataProtection-Keys` on a
  volume if that becomes a problem.
