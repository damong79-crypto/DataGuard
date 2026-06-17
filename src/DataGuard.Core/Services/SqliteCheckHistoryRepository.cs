using System.Text;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;
using Microsoft.Data.Sqlite;

namespace DataGuard.Core.Services;

/// <summary>
/// 체크 이력을 로컬 SQLite 파일에 누적 저장한다(PRD 기능 ⑥).
/// 단일 사용자 데스크탑 앱이므로 로컬 파일 DB로 충분하다.
/// </summary>
public sealed class SqliteCheckHistoryRepository : ICheckHistoryRepository
{
    private readonly string _connectionString;

    public SqliteCheckHistoryRepository(string? dbPath = null)
    {
        dbPath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataGuard",
            "history.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ConnectionString;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS CheckResults (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                QueryId      TEXT    NOT NULL,
                QueryName    TEXT    NOT NULL,
                ExecutedAt   TEXT    NOT NULL,
                Status       INTEGER NOT NULL,
                RowCount     INTEGER NOT NULL,
                ErrorMessage TEXT,
                DurationMs   INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_CheckResults_QueryId_ExecutedAt
                ON CheckResults (QueryId, ExecutedAt DESC);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsync(CheckResult result, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO CheckResults
                (QueryId, QueryName, ExecutedAt, Status, RowCount, ErrorMessage, DurationMs)
            VALUES
                ($queryId, $queryName, $executedAt, $status, $rowCount, $errorMessage, $durationMs);
            """;
        command.Parameters.AddWithValue("$queryId", result.QueryId.ToString());
        command.Parameters.AddWithValue("$queryName", result.QueryName);
        command.Parameters.AddWithValue("$executedAt", result.ExecutedAt.ToString("O"));
        command.Parameters.AddWithValue("$status", (int)result.Status);
        command.Parameters.AddWithValue("$rowCount", result.RowCount);
        command.Parameters.AddWithValue("$errorMessage", (object?)result.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$durationMs", result.DurationMs);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CheckResult>> GetRecentAsync(
        Guid queryId, int limit = 100, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, QueryId, QueryName, ExecutedAt, Status, RowCount, ErrorMessage, DurationMs
            FROM CheckResults
            WHERE QueryId = $queryId
            ORDER BY ExecutedAt DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$queryId", queryId.ToString());
        command.Parameters.AddWithValue("$limit", limit);

        return await ReadResultsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CheckResult>> GetRecentAcrossAllAsync(
        int limit = 200, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, QueryId, QueryName, ExecutedAt, Status, RowCount, ErrorMessage, DurationMs
            FROM CheckResults
            ORDER BY ExecutedAt DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        return await ReadResultsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CheckResult>> QueryAsync(
        HistoryFilter filter, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();

        // 지정된 조건만 WHERE 절에 동적으로 덧붙인다.
        var sql = new StringBuilder(
            "SELECT Id, QueryId, QueryName, ExecutedAt, Status, RowCount, ErrorMessage, DurationMs " +
            "FROM CheckResults WHERE 1=1");

        if (filter.QueryId is { } queryId)
        {
            sql.Append(" AND QueryId = $queryId");
            command.Parameters.AddWithValue("$queryId", queryId.ToString());
        }

        if (filter.Status is { } status)
        {
            sql.Append(" AND Status = $status");
            command.Parameters.AddWithValue("$status", (int)status);
        }

        if (filter.Since is { } since)
        {
            // ExecutedAt은 ISO 8601("O")로 저장된다. 동일 오프셋 환경에서 사전식 비교가 시간순과 일치.
            sql.Append(" AND ExecutedAt >= $since");
            command.Parameters.AddWithValue("$since", since.ToString("O"));
        }

        sql.Append(" ORDER BY ExecutedAt DESC LIMIT $limit");
        command.Parameters.AddWithValue("$limit", filter.Limit);
        command.CommandText = sql.ToString();

        return await ReadResultsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CheckResults WHERE ExecutedAt < $cutoff;";
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));

        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<CheckResult>> ReadResultsAsync(
        SqliteCommand command, CancellationToken cancellationToken)
    {
        var results = new List<CheckResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            results.Add(new CheckResult
            {
                Id = reader.GetInt64(0),
                QueryId = Guid.Parse(reader.GetString(1)),
                QueryName = reader.GetString(2),
                ExecutedAt = DateTimeOffset.Parse(reader.GetString(3)),
                Status = (CheckStatus)reader.GetInt32(4),
                RowCount = reader.GetInt64(5),
                ErrorMessage = reader.IsDBNull(6) ? null : reader.GetString(6),
                DurationMs = reader.GetInt64(7)
            });
        }

        return results;
    }
}
