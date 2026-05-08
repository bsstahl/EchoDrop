using EchoDrop.Domain.Models;
using EchoDrop.Domain.Interfaces;
using EchoDrop.Storage.Sqlite.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace EchoDrop.Storage.Sqlite;

public sealed class SqliteScheduledPostRepository(IOptions<DatabaseOptions> options) : IScheduledPostRepository
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE IF NOT EXISTS ScheduledPosts (
                    -- Client supplies GUID Id values for idempotent scheduling.
                    Id TEXT PRIMARY KEY,
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
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public Task UpsertAsync(ScheduledPost post, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(post);

        return UpsertAsync([post], cancellationToken);
    }

    public async Task UpsertAsync(IReadOnlyList<ScheduledPost> posts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(posts);

        if (posts.Count == 0)
        {
            return;
        }

        var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var post in posts)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT INTO ScheduledPosts (Id, Content, ScheduledAtUtc, PublishedAtUtc, ProviderPostId)
                        VALUES ($id, $content, $scheduledAtUtc, NULL, NULL)
                        ON CONFLICT(Id) DO UPDATE SET
                            Content = excluded.Content,
                            ScheduledAtUtc = excluded.ScheduledAtUtc;
                        """;
                    command.Parameters.AddWithValue("$id", post.Id.ToString("D", CultureInfo.InvariantCulture));
                    command.Parameters.AddWithValue("$content", post.Content);
                    command.Parameters.AddWithValue("$scheduledAtUtc", post.ScheduledAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ScheduledPost>> GetDuePostsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT Id, Content, ScheduledAtUtc
                FROM ScheduledPosts
                WHERE PublishedAtUtc IS NULL
                  AND ScheduledAtUtc <= $asOfUtc
                ORDER BY ScheduledAtUtc, Id;
                """;
            command.Parameters.AddWithValue("$asOfUtc", asOfUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));

            var duePosts = new List<ScheduledPost>();
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    duePosts.Add(
                        new ScheduledPost(
                            Guid.Parse(reader.GetString(0)),
                            reader.GetString(1),
                            DateTimeOffset.ParseExact(
                                reader.GetString(2),
                                "O",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.RoundtripKind)));
                }

                return duePosts;
            }
            finally
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    public async Task MarkAsPublishedAsync(Guid postId, string? providerPostId, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE ScheduledPosts
                SET PublishedAtUtc = $publishedAtUtc,
                    ProviderPostId = $providerPostId
                WHERE Id = $postId;
                """;
            command.Parameters.AddWithValue("$publishedAtUtc", publishedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$providerPostId", providerPostId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("$postId", postId.ToString("D", CultureInfo.InvariantCulture));

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}
