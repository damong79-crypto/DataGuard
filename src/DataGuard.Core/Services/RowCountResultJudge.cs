using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// PRD 3단계 A의 판정 규칙 구현:
/// 실행 실패 → Error, 결과 0건 → Normal, 1건 이상 → Anomaly.
/// </summary>
public sealed class RowCountResultJudge : IResultJudge
{
    public CheckStatus Judge(QueryExecutionOutcome outcome)
    {
        if (!outcome.Succeeded)
        {
            return CheckStatus.Error;
        }

        return outcome.RowCount == 0 ? CheckStatus.Normal : CheckStatus.Anomaly;
    }
}
