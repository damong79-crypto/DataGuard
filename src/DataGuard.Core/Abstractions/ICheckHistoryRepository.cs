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

    /// <summary>쿼리 구분 없이 가장 최근 실행 이력을 모아서 조회한다(이력 탭 전체 보기용).</summary>
    Task<IReadOnlyList<CheckResult>> GetRecentAcrossAllAsync(
        int limit = 200,
        CancellationToken cancellationToken = default);

    /// <summary>쿼리·상태·기간 등 조건으로 이력을 조회한다(이력 탭 필터용).</summary>
    Task<IReadOnlyList<CheckResult>> QueryAsync(
        HistoryFilter filter,
        CancellationToken cancellationToken = default);
}
