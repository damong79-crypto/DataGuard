using DataGuard.Core.Models;

namespace DataGuard.Core.Abstractions;

/// <summary>체크 결과를 수신자에게 이메일로 발송한다. 본문엔 건수·요약만 담는다(PRD 4단계 A).</summary>
public interface IEmailNotifier
{
    Task SendAsync(
        SmtpSettings settings,
        string smtpPassword,
        IReadOnlyCollection<string> recipients,
        string subject,
        string body,
        CancellationToken cancellationToken = default);
}
