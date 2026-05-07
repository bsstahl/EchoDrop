using EchoDrop.Configuration;
using EchoDrop.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EchoDrop.Storage;

public sealed class SqliteScheduledPostRepository(IOptions<DatabaseOptions> options) : IScheduledPostRepository
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS ScheduledPosts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Content TEXT NOT NULL,
                ScheduledAtUtc TEXT NOT NULL,
                PublishedAtUtc TEXT NULL,
                ProviderPostId TEXT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ScheduledPosts_Due
            ON ScheduledPosts(PublishedAtUtc, ScheduledAtUtc);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, Content, ScheduledAtUtc
            FROM ScheduledPosts
            WHERE PublishedAtUtc IS NULL
              AND ScheduledAtUtc <= $asOfUtc
            ORDER BY ScheduledAtUtc, Id;
            """;
        command.Parameters.AddWithValue("$asOfUtc", asOfUtc.UtcDateTime.ToString("O"));

        var duePosts = new List<ScheduledPost>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            duePosts.Add(
                new ScheduledPost(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    DateTimeOffset.Parse(reader.GetString(2))));
        }

        return duePosts;
    }

    public async Task MarkAsPublishedAsync(long postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE ScheduledPosts
            SET PublishedAtUtc = $publishedAtUtc,
                ProviderPostId = $providerPostId
            WHERE Id = $postId;
            """;
        command.Parameters.AddWithValue("$publishedAtUtc", publishedAtUtc.UtcDateTime.ToString("O"));
        command.Parameters.AddWithValue("$providerPostId", providerPostId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$postId", postId);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
