using System.Globalization;
using EchoDrop.Storage.Sqlite.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace EchoDrop.Storage.Sqlite.Tests;

public sealed class SqliteScheduledPostRepositoryTests
{
    [Fact]
    public async Task EnsureSchemaAsync_CreatesScheduledPostsTableAndIndex()
    {
        using var db = new TemporaryDatabase();
        var sut = db.CreateRepository();

        await sut.EnsureSchemaAsync(CancellationToken.None);

        await using var connection = db.OpenConnection();
        await connection.OpenAsync();

        Assert.Equal("ScheduledPosts", await GetSqliteMasterNameAsync(connection, "table", "ScheduledPosts"));
        Assert.Equal("IX_ScheduledPosts_Due", await GetSqliteMasterNameAsync(connection, "index", "IX_ScheduledPosts_Due"));
        Assert.Equal("TEXT", await GetColumnTypeAsync(connection, "Id"));
    }

    [Fact]
    public async Task GetDuePostsAsync_ReturnsOnlyUnpublishedDuePostsInOrder()
    {
        using var db = new TemporaryDatabase();
        var sut = db.CreateRepository();
        await sut.EnsureSchemaAsync(CancellationToken.None);

        var asOfUtc = new DateTimeOffset(2026, 01, 15, 12, 0, 0, TimeSpan.Zero);
        var firstDueId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondDueId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        await SeedPostAsync(db, firstDueId, "first due", new DateTimeOffset(2026, 01, 15, 10, 0, 0, TimeSpan.Zero), null, null);
        await SeedPostAsync(db, secondDueId, "second due", new DateTimeOffset(2026, 01, 15, 10, 0, 0, TimeSpan.Zero), null, null);
        await SeedPostAsync(db, Guid.Parse("10000000-0000-0000-0000-000000000003"), "future", new DateTimeOffset(2026, 01, 15, 13, 0, 0, TimeSpan.Zero), null, null);
        await SeedPostAsync(db, Guid.Parse("10000000-0000-0000-0000-000000000004"), "already published", new DateTimeOffset(2026, 01, 15, 9, 0, 0, TimeSpan.Zero), asOfUtc, "provider-id");

        var result = await sut.GetDuePostsAsync(asOfUtc, CancellationToken.None);

        Assert.Collection(
            result,
            post =>
            {
                Assert.Equal(firstDueId, post.Id);
                Assert.Equal("first due", post.Content);
                Assert.Equal(new DateTimeOffset(2026, 01, 15, 10, 0, 0, TimeSpan.Zero), post.ScheduledAtUtc);
            },
            post =>
            {
                Assert.Equal(secondDueId, post.Id);
                Assert.Equal("second due", post.Content);
                Assert.Equal(new DateTimeOffset(2026, 01, 15, 10, 0, 0, TimeSpan.Zero), post.ScheduledAtUtc);
            });
    }

    [Fact]
    public async Task MarkAsPublishedAsync_SetsPublishedTimestampAndProviderId()
    {
        using var db = new TemporaryDatabase();
        var sut = db.CreateRepository();
        await sut.EnsureSchemaAsync(CancellationToken.None);
        var postId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        await SeedPostAsync(db, postId, "to publish", new DateTimeOffset(2026, 01, 15, 9, 0, 0, TimeSpan.Zero), null, null);

        var publishedAtUtc = new DateTimeOffset(2026, 01, 15, 12, 30, 0, TimeSpan.Zero);
        await sut.MarkAsPublishedAsync(postId, "provider-123", publishedAtUtc, CancellationToken.None);

        await using var connection = db.OpenConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT PublishedAtUtc, ProviderPostId FROM ScheduledPosts WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", postId.ToString("D", CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(publishedAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture), reader.GetString(0));
        Assert.Equal("provider-123", reader.GetString(1));
    }

    [Fact]
    public async Task MarkAsPublishedAsync_StoresNullProviderPostIdWhenMissing()
    {
        using var db = new TemporaryDatabase();
        var sut = db.CreateRepository();
        await sut.EnsureSchemaAsync(CancellationToken.None);
        var postId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        await SeedPostAsync(db, postId, "to publish", new DateTimeOffset(2026, 01, 15, 9, 0, 0, TimeSpan.Zero), null, null);

        await sut.MarkAsPublishedAsync(postId, null, new DateTimeOffset(2026, 01, 15, 12, 30, 0, TimeSpan.Zero), CancellationToken.None);

        await using var connection = db.OpenConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ProviderPostId FROM ScheduledPosts WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", postId.ToString("D", CultureInfo.InvariantCulture));
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.IsDBNull(0));
    }

    [Fact]
    public void DatabaseOptions_HasExpectedDefaults()
    {
        var options = new DatabaseOptions();

        Assert.Equal("Database", DatabaseOptions.SectionName);
        Assert.Equal("Data Source=echodrop.db", options.ConnectionString);
    }

    private static async Task SeedPostAsync(
        TemporaryDatabase db,
        Guid id,
        string content,
        DateTimeOffset scheduledAtUtc,
        DateTimeOffset? publishedAtUtc,
        string? providerPostId)
    {
        await using var connection = db.OpenConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO ScheduledPosts (Id, Content, ScheduledAtUtc, PublishedAtUtc, ProviderPostId)
            VALUES ($id, $content, $scheduledAtUtc, $publishedAtUtc, $providerPostId);
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$content", content);
        command.Parameters.AddWithValue("$scheduledAtUtc", scheduledAtUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$publishedAtUtc", publishedAtUtc?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$providerPostId", providerPostId ?? (object)DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> GetSqliteMasterNameAsync(SqliteConnection connection, string type, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT name
            FROM sqlite_master
            WHERE type = $type AND name = $name;
            """;
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$name", name);
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<string?> GetColumnTypeAsync(SqliteConnection connection, string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(ScheduledPosts);";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.Ordinal))
            {
                return reader.GetString(2);
            }
        }

        return null;
    }

    private sealed class TemporaryDatabase : IDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.db");
        private readonly string _connectionString;

        public TemporaryDatabase()
        {
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _databasePath,
                Pooling = false
            }.ToString();
        }

        public SqliteScheduledPostRepository CreateRepository()
            => new(Options.Create(new DatabaseOptions { ConnectionString = _connectionString }));

        public SqliteConnection OpenConnection()
            => new(_connectionString);

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
    }
}
