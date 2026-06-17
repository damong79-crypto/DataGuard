namespace DataGuard.Core.Models;

/// <summary>
/// 체크 실행 1건의 이력 레코드. 결과 이력 저장소(SQLite)에 누적되어
/// "언제부터 틀어졌는지" 추적과 인수인계를 가능하게 한다(PRD 1·5단계).
/// </summary>
public sealed class CheckResult
{
    /// <summary>저장소 자동 증가 PK(저장 전에는 0).</summary>
    public long Id { get; set; }

    public Guid QueryId { get; set; }

    public string QueryName { get; set; } = string.Empty;

    public DateTimeOffset ExecutedAt { get; set; }

    public CheckStatus Status { get; set; }

    public long RowCount { get; set; }

    public string? ErrorMessage { get; set; }

    public long DurationMs { get; set; }
}
