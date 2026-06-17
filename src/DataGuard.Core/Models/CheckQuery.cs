namespace DataGuard.Core.Models;

/// <summary>
/// 등록자가 등록하는 정합성 체크 쿼리.
/// 판정 규칙은 고정: 결과 0건=정상, 1건 이상=이상(PRD 3단계 A).
/// 따라서 SQL은 "틀어진 행만 뽑아내는" 형태로 작성한다.
/// </summary>
public sealed class CheckQuery
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>이 쿼리를 실행할 대상 연결(<see cref="DbConnectionInfo.Id"/>).</summary>
    public Guid ConnectionId { get; set; }

    public string Sql { get; set; } = string.Empty;

    public NotifyPolicy NotifyPolicy { get; set; } = NotifyPolicy.OnAnomalyOnly;

    /// <summary>
    /// 자동 실행 스케줄. MVP 이후 단계 기능이므로 null이면 수동 실행만 가능.
    /// 표현식 형식(cron/간격)은 스케줄러 구현에서 확정한다.
    /// </summary>
    public string? Schedule { get; set; }

    public bool IsEnabled { get; set; } = true;
}
