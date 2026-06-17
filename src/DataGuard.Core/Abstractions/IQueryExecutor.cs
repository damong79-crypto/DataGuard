using DataGuard.Core.Models;

namespace DataGuard.Core.Abstractions;

/// <summary>대상 DB에 체크 쿼리를 실행하고 원시 결과를 반환한다.</summary>
public interface IQueryExecutor
{
    /// <param name="connection">대상 연결 정보(비밀번호 제외).</param>
    /// <param name="password">자격 증명 저장소에서 꺼낸 비밀번호(메모리상에서만 사용).</param>
    /// <param name="sql">실행할 정합성 체크 쿼리.</param>
    Task<QueryExecutionOutcome> ExecuteAsync(
        DbConnectionInfo connection,
        string password,
        string sql,
        CancellationToken cancellationToken = default);
}
