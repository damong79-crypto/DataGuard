using System.Data.Common;
using System.Diagnostics;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace DataGuard.Core.Services;

/// <summary>
/// 대상 DB에 체크 쿼리를 실행하고 반환 행 수를 집계한다.
/// SQL Server·PostgreSQL 두 드라이버를 공통 ADO.NET(DbConnection) 인터페이스로 다룬다.
/// </summary>
public sealed class QueryExecutor : IQueryExecutor
{
    // 잘못 등록된 쿼리가 운영 DB에 장시간 부하를 주지 않도록 기본 타임아웃을 둔다(PRD 위험 ③).
    private const int CommandTimeoutSeconds = 30;

    public async Task<QueryExecutionOutcome> ExecuteAsync(
        DbConnectionInfo connection,
        string password,
        string sql,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return QueryExecutionOutcome.Failure("실행할 SQL이 비어 있습니다.", TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using DbConnection db = CreateConnection(connection, password);
            await db.OpenAsync(cancellationToken).ConfigureAwait(false);

            await using DbCommand command = db.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = CommandTimeoutSeconds;

            // 판정에는 행의 "존재 여부 + 건수"만 필요하므로 값 자체는 읽지 않는다.
            // 정합성 쿼리는 보통 소수의 "틀어진 행"만 반환하나, 대량 반환 가능성에 대비해
            // 스트리밍 리더로 카운트만 누적한다(전체를 메모리에 올리지 않음).
            await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);

            long rowCount = 0;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rowCount++;
            }

            stopwatch.Stop();
            return QueryExecutionOutcome.Success(rowCount, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            // 취소는 호출자가 처리하도록 그대로 전파.
            throw;
        }
        catch (DbException ex)
        {
            // 연결 실패·문법 오류·타임아웃 등은 "오류" 상태로 판정되도록 결과에 담아 반환.
            stopwatch.Stop();
            return QueryExecutionOutcome.Failure(ex.Message, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return QueryExecutionOutcome.Failure($"예기치 못한 오류: {ex.Message}", stopwatch.Elapsed);
        }
    }

    private static DbConnection CreateConnection(DbConnectionInfo info, string password) =>
        info.Provider switch
        {
            DbProvider.SqlServer => new SqlConnection(BuildSqlServerConnectionString(info, password)),
            DbProvider.PostgreSql => new NpgsqlConnection(BuildPostgreSqlConnectionString(info, password)),
            _ => throw new NotSupportedException($"지원하지 않는 DB 종류: {info.Provider}")
        };

    private static string BuildSqlServerConnectionString(DbConnectionInfo info, string password)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = info.Port > 0 ? $"{info.Host},{info.Port}" : info.Host,
            InitialCatalog = info.Database,
            UserID = info.Username,
            Password = password,
            ConnectTimeout = 15,
            // 사내 환경 인증서 이슈를 피하기 위한 기본값. 운영 보안 정책에 맞춰 재검토 필요.
            TrustServerCertificate = true
        };
        return builder.ConnectionString;
    }

    private static string BuildPostgreSqlConnectionString(DbConnectionInfo info, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = info.Host,
            Port = info.Port > 0 ? info.Port : 5432,
            Database = info.Database,
            Username = info.Username,
            Password = password,
            Timeout = 15
        };
        return builder.ConnectionString;
    }
}
