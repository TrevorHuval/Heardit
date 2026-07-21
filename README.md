# Heardit

Letterboxd for music — rate tracks, follow people, keep a listen-later queue.

## what it does

Search Spotify's catalog for a track, or browse new releases on the home page. Rate a track
1–10 and write a review; reviewing the same track again edits the review you already wrote.

Follow other listeners and the home page grows a Following tab: their reviews, newest first.
Like a review from anywhere it appears. Save tracks to a listen-later queue from a feed card or
from the track page. Profiles carry a bio, up to four favorite tracks, and everything the person
has reviewed. Search matches usernames as well as tracks, so it doubles as the way to find people.

Every page is behind a login. There is no anonymous view.

## stack

- ASP.NET Core 10 MVC with Razor views
- PostgreSQL through EF Core (Npgsql); migrations run at startup
- ASP.NET Core Identity for accounts
- SpotifyAPI.Web on the client-credentials flow — the app has its own Spotify app, it never
  connects to a listener's Spotify account
- xUnit, NSubstitute, SQLite in-memory as the test provider
- Docker Compose to run the app and its database together

No frontend framework: one CSS file of design tokens (`wwwroot/css/site.css`) and one short
script. Follow, like and save are plain form POSTs, so they work without JavaScript.

## architecture

Controllers are thin — resolve the current user, call a service, hand a view model to a view.

- `Services/` — `SpotifyService` (every Spotify call, behind a size-bounded `IMemoryCache`),
  `SongService` (writes the local `Songs` row the first time anyone touches a track),
  `ReviewService` (reviews, likes, the following feed), `ProfileService` (profiles, follows, bio,
  favorites, user search), `ListenLaterService` (the queue). `FeedStats` and `SavedState` are
  helpers the controllers call to batch per-card state into one query instead of one per card.
- `Models/` — EF entities. `HearditDbContext` holds the relationship config; every row a user
  owns cascades when the account is deleted.
- `ViewModels/` — one per page, plus `PagedList<T>`, which is the `?page=` pagination used by
  profiles, song pages, follow lists, the feed and the queue.
- `Views/` — Razor views and shared partials. `Areas/Identity` is the scaffolded Identity UI.

Track data flows one way: a Spotify id goes to `SpotifyService` for metadata (cached), and
`SongService.GetOrCreateSongAsync` persists a minimal `Songs` row the first time someone reviews
or saves it. Reviews, likes, queue entries and favorites all reference that id. Only tracks
somebody has acted on exist locally; everything else stays a Spotify call.

Authorization defaults to authenticated (fallback policy in `Program.cs`), so a new action is
login-walled unless it opts out. Unsafe requests are antiforgery-validated, actions that call
Spotify sit behind a per-user rate limiter, and the CSP allows self plus Spotify's embed hosts.

## running it locally

Either path needs your own Spotify app credentials from
https://developer.spotify.com/dashboard. Startup validates them and refuses to boot without them.

### docker compose

```
cd Heardit
cp .env.example .env        # fill in POSTGRES_PASSWORD and the Spotify credentials
docker compose up --build
```

Serves http://localhost:8080 with its own Postgres. Details and environment variables are in
[Heardit/docs/DEPLOY.md](Heardit/docs/DEPLOY.md).

### dotnet run

Needs a Postgres on localhost:5432:

```
docker run -d --name heardit-pg -e POSTGRES_PASSWORD=<password> -e POSTGRES_DB=heardit \
  -p 5432:5432 -v heardit-pgdata:/var/lib/postgresql/data postgres:17-alpine
```

`appsettings.json` carries a password-free connection string, so put the real one and the Spotify
credentials in user-secrets:

```
cd Heardit
dotnet user-secrets set "ConnectionStrings:HearditDbContextConnection" \
  "Host=localhost;Port=5432;Database=heardit;Username=postgres;Password=<password>"
dotnet user-secrets set "Spotify:ClientId" "<id>"
dotnet user-secrets set "Spotify:ClientSecret" "<secret>"
dotnet run
```

Serves http://localhost:5046. Migrations apply on startup, so the database creates itself.

## tests

`dotnet test` from the repo root.

## deploy

See [Heardit/docs/DEPLOY.md](Heardit/docs/DEPLOY.md) — configuration, compose, and running behind
a TLS-terminating reverse proxy.

## known limitations

- **No password recovery.** Nothing here sends email, so the reset flow is gone rather than
  broken. A forgotten password can only be reset by whoever has database access.
- **The home page's new-releases feed uses Spotify endpoints the SDK marks obsolete**
  (`Browse.GetNewReleases`, `Albums.GetSeveral`). They still work; if the home feed empties out or
  starts failing, that is the first place to look.
- **An account is required for everything** — no public profiles, no shareable review links.
- **Favorites cannot be reordered.** Removing one frees its slot and the next favorite fills it.
- **The listen-later queue is private** and has no way to share or export it.
- Sorting a song page's reviews sorts within the page you are on, not across all of them.
