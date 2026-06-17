using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// 체크 1건의 전체 흐름을 묶는 오케스트레이터(MVP 핵심 고리):
/// 비밀번호 조회 → 쿼리 실행 → 0건 판정 → 이력 저장 → 정책에 따른 이메일 발송.
/// 수동 실행("지금 실행")과 스케줄 실행이 모두 이 경로를 공유한다.
/// </summary>
public sealed class CheckRunner
{
    private readonly IQueryExecutor _executor;
    private readonly IResultJudge _judge;
    private readonly ICredentialStore _credentials;
    private readonly ICheckHistoryRepository _history;
    private readonly IEmailNotifier _notifier;

    public CheckRunner(
        IQueryExecutor executor,
        IResultJudge judge,
        ICredentialStore credentials,
        ICheckHistoryRepository history,
        IEmailNotifier notifier)
    {
        _executor = executor;
        _judge = judge;
        _credentials = credentials;
        _history = history;
        _notifier = notifier;
    }

    /// <param name="recipients">결과를 받을 수신자 이메일.</param>
    /// <param name="smtp">발송 설정(없으면 알림 생략).</param>
    public async Task<CheckResult> RunAsync(
        CheckQuery query,
        DbConnectionInfo connection,
        IReadOnlyCollection<string> recipients,
        SmtpSettings? smtp,
        CancellationToken cancellationToken = default)
    {
        string password = _credentials.Retrieve(connection.CredentialKey)
            ?? throw new InvalidOperationException(
                $"연결 '{connection.Name}'의 비밀번호가 자격 증명 저장소에 없습니다.");

        QueryExecutionOutcome outcome =
            await _executor.ExecuteAsync(connection, password, query.Sql, cancellationToken)
                .ConfigureAwait(false);

        CheckStatus status = _judge.Judge(outcome);

        var result = new CheckResult
        {
            QueryId = query.Id,
            QueryName = query.Name,
            ExecutedAt = DateTimeOffset.Now,
            Status = status,
            RowCount = outcome.RowCount,
            ErrorMessage = outcome.ErrorMessage,
            DurationMs = (long)outcome.Duration.TotalMilliseconds
        };

        await _history.SaveAsync(result, cancellationToken).ConfigureAwait(false);

        if (ShouldNotify(query.NotifyPolicy, status) && smtp is not null && recipients.Count > 0)
        {
            await NotifyAsync(smtp, recipients, query, result, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    // 발송 정책(PRD 3단계 C): 이상일 때만 / 항상. 오류는 정책과 무관하게 항상 알린다.
    private static bool ShouldNotify(NotifyPolicy policy, CheckStatus status)
    {
        if (status == CheckStatus.Error)
        {
            return true;
        }

        return policy switch
        {
            NotifyPolicy.Always => true,
            NotifyPolicy.OnAnomalyOnly => status == CheckStatus.Anomaly,
            _ => false
        };
    }

    private async Task NotifyAsync(
        SmtpSettings smtp,
        IReadOnlyCollection<string> recipients,
        CheckQuery query,
        CheckResult result,
        CancellationToken cancellationToken)
    {
        string statusLabel = result.Status switch
        {
            CheckStatus.Normal => "정상(이상 없음)",
            CheckStatus.Anomaly => $"이상 발견 — {result.RowCount}건",
            CheckStatus.Error => "실행 오류",
            _ => result.Status.ToString()
        };

        string subject = $"[DataGuard] {query.Name} — {statusLabel}";

        // 본문엔 건수·요약만 담는다(PRD 4단계 A: 민감값 미포함).
        string body =
            $"""
             체크: {query.Name}
             실행 시각: {result.ExecutedAt:yyyy-MM-dd HH:mm:ss}
             상태: {statusLabel}
             소요: {result.DurationMs}ms
             {(result.ErrorMessage is null ? string.Empty : $"오류: {result.ErrorMessage}")}
             """;

        await _notifier.SendAsync(smtp, smtpPassword: ResolveSmtpPassword(), recipients, subject, body, cancellationToken)
            .ConfigureAwait(false);
    }

    private string ResolveSmtpPassword() =>
        _credentials.Retrieve(SmtpSettings.CredentialKey)
            ?? throw new InvalidOperationException("SMTP 비밀번호가 자격 증명 저장소에 없습니다.");
}
