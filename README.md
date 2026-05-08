# EchoDrop

EchoDrop is a .NET 10 worker service that polls a local SQLite database for due scheduled posts and publishes them to Mastodon.

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
