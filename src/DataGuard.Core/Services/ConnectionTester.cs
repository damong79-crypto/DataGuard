using System.Data.Common;
using System.Diagnostics;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>대상 DB에 실제로 접속을 시도해 연결 가능 여부를 확인한다.</summary>
public sealed class ConnectionTester : IConnectionTester
{
    public async Task<ConnectionTestResult> TestAsync(
        DbConnectionInfo connection,
        string password,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using DbConnection db = DbConnectionFactory.Create(connection, password);
            await db.OpenAsync(cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            return ConnectionTestResult.Success(stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return ConnectionTestResult.Failure(ex.Message, stopwatch.Elapsed);
        }
    }
}
