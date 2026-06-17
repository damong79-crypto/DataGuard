using DataGuard.Core.Models;

namespace DataGuard.Core.Abstractions;

/// <summary>체크 실행 결과를 누적 저장하고 조회한다(PRD 기능 ⑥).</summary>
public interface ICheckHistoryRepository
{
    /// <summary>스키마 생성 등 최초 1회 초기화.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(CheckResult result, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckResult>> GetRecentAsync(
        Guid queryId,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
