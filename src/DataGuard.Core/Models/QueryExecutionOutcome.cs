namespace DataGuard.Core.Models;

/// <summary>
/// 쿼리 1회 실행의 원시 결과. 판정(<see cref="CheckStatus"/>)은 별도로
/// <c>IResultJudge</c>가 이 결과를 보고 내린다(실행과 판정의 책임 분리).
/// </summary>
public sealed record QueryExecutionOutcome
{
    /// <summary>실행 자체의 성공 여부(연결·문법·타임아웃 오류가 없었는가).</summary>
    public required bool Succeeded { get; init; }

    /// <summary>반환된 행 수. 0이면 정상, 1 이상이면 이상으로 판정된다.</summary>
    public long RowCount { get; init; }

    /// <summary>실패 시 원인 메시지(성공이면 null).</summary>
    public string? ErrorMessage { get; init; }

    public TimeSpan Duration { get; init; }

    public static QueryExecutionOutcome Success(long rowCount, TimeSpan duration) =>
        new() { Succeeded = true, RowCount = rowCount, Duration = duration };

    public static QueryExecutionOutcome Failure(string error, TimeSpan duration) =>
        new() { Succeeded = false, ErrorMessage = error, Duration = duration };
}
