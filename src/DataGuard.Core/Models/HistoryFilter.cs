namespace DataGuard.Core.Models;

/// <summary>이력 조회 필터. 각 항목이 null이면 해당 조건은 적용하지 않는다(전체).</summary>
public sealed class HistoryFilter
{
    public Guid? QueryId { get; set; }

    public CheckStatus? Status { get; set; }

    /// <summary>이 시각 이후의 실행만 조회.</summary>
    public DateTimeOffset? Since { get; set; }

    public int Limit { get; set; } = 500;
}
