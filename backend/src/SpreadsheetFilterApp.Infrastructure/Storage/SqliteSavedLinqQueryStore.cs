using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using SpreadsheetFilterApp.Application.Abstractions.Persistence;

namespace SpreadsheetFilterApp.Infrastructure.Storage;

public sealed class SqliteSavedLinqQueryStore : ISavedLinqQueryStore
{
    private const string DateFormat = "O";

    private readonly string _connectionString;

    public SqliteSavedLinqQueryStore(IOptions<SavedLinqQueryStoreOptions> options)
    {
        var configuredPath = options.Value.SqlitePath;
        var dbPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configuredPath));

        var dbDirectory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrWhiteSpace(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureDatabase();
    }

    public async Task<SavedLinqQuery> CreateAsync(string name, string linqCode, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO saved_linq_queries (name, linq_code, created_at_utc, updated_at_utc)
VALUES ($name, $linqCode, $createdAtUtc, $updatedAtUtc);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$linqCode", linqCode);
        command.Parameters.AddWithValue("$createdAtUtc", utcNow.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$updatedAtUtc", utcNow.ToString(DateFormat, CultureInfo.InvariantCulture));

        var insertedId = (long)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("Failed to insert saved LINQ query."));

        return new SavedLinqQuery
        {
            Id = insertedId,
            Name = name,
            LinqCode = linqCode,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };
    }

    public async Task<IReadOnlyList<SavedLinqQuery>> ListAsync(CancellationToken cancellationToken)
    {
        var items = new List<SavedLinqQuery>();

        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, linq_code, created_at_utc, updated_at_utc
FROM saved_linq_queries
ORDER BY updated_at_utc DESC, id DESC;";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(Map(reader));
        }

        return items;
    }

    public async Task<SavedLinqQuery?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, name, linq_code, created_at_utc, updated_at_utc
FROM saved_linq_queries
WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return Map(reader);
        }

        return null;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM saved_linq_queries WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    private SqliteConnection CreateConnection() => new(_connectionString);

    private void EnsureDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS saved_linq_queries (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    linq_code TEXT NOT NULL,
    created_at_utc TEXT NOT NULL,
    updated_at_utc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_saved_linq_queries_updated_at
    ON saved_linq_queries(updated_at_utc DESC, id DESC);";

        command.ExecuteNonQuery();
    }

    private static SavedLinqQuery Map(SqliteDataReader reader)
    {
        return new SavedLinqQuery
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            LinqCode = reader.GetString(2),
            CreatedAtUtc = ParseUtc(reader.GetString(3)),
            UpdatedAtUtc = ParseUtc(reader.GetString(4))
        };
    }

    private static DateTime ParseUtc(string value)
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
    }
}
