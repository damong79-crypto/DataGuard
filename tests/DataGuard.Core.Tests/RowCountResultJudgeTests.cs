using DataGuard.Core.Models;
using DataGuard.Core.Services;

namespace DataGuard.Core.Tests;

public class RowCountResultJudgeTests
{
    private readonly RowCountResultJudge _judge = new();

    [Fact]
    public void FailedExecution_IsError()
    {
        var outcome = QueryExecutionOutcome.Failure("연결 실패", TimeSpan.Zero);

        Assert.Equal(CheckStatus.Error, _judge.Judge(outcome));
    }

    [Fact]
    public void ZeroRows_IsNormal()
    {
        var outcome = QueryExecutionOutcome.Success(rowCount: 0, TimeSpan.Zero);

        Assert.Equal(CheckStatus.Normal, _judge.Judge(outcome));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    public void OneOrMoreRows_IsAnomaly(long rows)
    {
        var outcome = QueryExecutionOutcome.Success(rows, TimeSpan.Zero);

        Assert.Equal(CheckStatus.Anomaly, _judge.Judge(outcome));
    }
}
