using System.Net;
using System.Net.Mail;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// System.Net.Mail 기반 SMTP 발송. 본문엔 건수·요약만 담기므로 민감정보는 나가지 않는다(PRD 4단계 A).
///
/// 대안: MailKit — OAuth2·최신 SMTP 기능이 필요하면 교체 권장. 사내 SMTP 릴레이 대상이라
/// 기본 BCL(SmtpClient)로 충분하다고 보고 MVP에 채택.
/// </summary>
public sealed class SmtpEmailNotifier : IEmailNotifier
{
    public async Task SendAsync(
        SmtpSettings settings,
        string smtpPassword,
        IReadOnlyCollection<string> recipients,
        string subject,
        string body,
        CancellationToken cancellationToken = default)
    {
        if (recipients is null || recipients.Count == 0)
        {
            throw new ArgumentException("수신자가 한 명도 없습니다.", nameof(recipients));
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        foreach (string to in recipients)
        {
            message.To.Add(to);
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            Credentials = new NetworkCredential(settings.Username, smtpPassword)
        };

        // SmtpClient.SendMailAsync는 CancellationToken 오버로드(.NET 5+)를 지원.
        await client.SendMailAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
