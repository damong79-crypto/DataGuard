using DataGuard.Core.Models;

namespace DataGuard.Core.Abstractions;

/// <summary>등록 전 DB 접속이 가능한지 확인한다.</summary>
public interface IConnectionTester
{
    Task<ConnectionTestResult> TestAsync(
        DbConnectionInfo connection,
        string password,
        CancellationToken cancellationToken = default);
}
