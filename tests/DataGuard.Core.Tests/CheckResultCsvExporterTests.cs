using DataGuard.Core.Models;
using DataGuard.Core.Services;

namespace DataGuard.Core.Tests;

public class CheckResultCsvExporterTests
{
    private static readonly DateTimeOffset At =
        new(2026, 6, 17, 9, 30, 0, TimeSpan.FromHours(9));

    [Fact]
    public void StartsWithHeader()
    {
        string csv = CheckResultCsvExporter.ToCsv(Array.Empty<CheckResult>());

        Assert.StartsWith("쿼리,실행시각,상태,건수,소요(ms),오류\r\n", csv);
    }

    [Fact]
    public void WritesRowWithKoreanStatusLabel()
    {
        var result = new CheckResult
        {
            QueryName = "재고정합성",
            ExecutedAt = At,
            Status = CheckStatus.Anomaly,
            RowCount = 3,
            DurationMs = 120
        };

        string csv = CheckResultCsvExporter.ToCsv(new[] { result });

        Assert.Contains("재고정합성,2026-06-17 09:30:00,이상,3,120,\r\n", csv);
    }

    [Fact]
    public void EscapesCommaQuoteAndNewline()
    {
        var result = new CheckResult
        {
            QueryName = "a,b\"c",          // 쉼표 + 따옴표
            ExecutedAt = At,
            Status = CheckStatus.Error,
            RowCount = 0,
            DurationMs = 5,
            ErrorMessage = "line1\nline2"  // 줄바꿈
        };

        string csv = CheckResultCsvExporter.ToCsv(new[] { result });

        // 따옴표는 두 번으로, 필드 전체는 따옴표로 감싼다.
        Assert.Contains("\"a,b\"\"c\"", csv);
        Assert.Contains("\"line1\nline2\"", csv);
    }
}
