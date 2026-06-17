using DataGuard.Core.Models;

namespace DataGuard.Core.Abstractions;

/// <summary>실행 결과를 보고 정상/이상/오류를 판정한다.</summary>
public interface IResultJudge
{
    CheckStatus Judge(QueryExecutionOutcome outcome);
}
