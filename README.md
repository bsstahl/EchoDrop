# EchoDrop

EchoDrop is a .NET 10 worker service that polls a local SQLite database for due scheduled posts and publishes them to Mastodon.

## Data update API

EchoDrop exposes a REST API for idempotent scheduling updates:

- `PUT /api/posts/{id}` with JSON body `{ "content": "...", "scheduledAtUtc": "2026-01-15T12:00:00Z" }`
- `PUT /api/posts` with a JSON array of `{ "id": "...", "content": "...", "scheduledAtUtc": "..." }`

Both endpoints upsert by post id for later publication.

## Configuration

`src/EchoDrop/appsettings.json`:

- `Database:ConnectionString` - SQLite connection string (default `Data Source=echodrop.db`)
- `Worker:PollIntervalSeconds` - polling interval for due posts
- `Mastodon:BaseUrl` - Mastodon instance base URL
- `Mastodon:AccessToken` - Mastodon API access token

## Scheduled post schema

The service creates the `ScheduledPosts` table automatically at startup.

Required columns used for scheduling/publishing:

- `Id` (TEXT PRIMARY KEY, GUID supplied by client for idempotence)
- `Content` (TEXT NOT NULL)
- `ScheduledAtUtc` (TEXT, ISO-8601 UTC timestamp)
- `PublishedAtUtc` (TEXT NULL)
- `ProviderPostId` (TEXT NULL)
