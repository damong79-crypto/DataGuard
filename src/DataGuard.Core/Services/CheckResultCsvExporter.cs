using System.Text;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>체크 이력을 CSV 문자열로 변환한다(RFC 4180 방식 이스케이프).</summary>
public static class CheckResultCsvExporter
{
    private const string Header = "쿼리,실행시각,상태,건수,소요(ms),오류";

    public static string ToCsv(IEnumerable<CheckResult> results)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n");

        foreach (CheckResult r in results)
        {
            builder
                .Append(Escape(r.QueryName)).Append(',')
                .Append(Escape(r.ExecutedAt.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',')
                .Append(Escape(StatusLabel(r.Status))).Append(',')
                .Append(r.RowCount).Append(',')
                .Append(r.DurationMs).Append(',')
                .Append(Escape(r.ErrorMessage ?? string.Empty))
                .Append("\r\n");
        }

        return builder.ToString();
    }

    // 쉼표·따옴표·줄바꿈이 있으면 따옴표로 감싸고 내부 따옴표는 두 번으로 이스케이프.
    private static string Escape(string field)
    {
        if (field.Contains('"') || field.Contains(',') ||
            field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    private static string StatusLabel(CheckStatus status) => status switch
    {
        CheckStatus.Normal => "정상",
        CheckStatus.Anomaly => "이상",
        CheckStatus.Error => "오류",
        _ => status.ToString()
    };
}
