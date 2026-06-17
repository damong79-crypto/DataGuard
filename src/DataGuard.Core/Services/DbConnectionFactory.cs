using System.Data.Common;
using DataGuard.Core.Models;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace DataGuard.Core.Services;

/// <summary>
/// DbConnectionInfo + 비밀번호로 provider별 ADO.NET 연결을 만든다.
/// 쿼리 실행기와 연결 테스터가 공유한다(연결 문자열 구성 로직 일원화).
/// </summary>
public static class DbConnectionFactory
{
    public static DbConnection Create(DbConnectionInfo info, string password) =>
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
