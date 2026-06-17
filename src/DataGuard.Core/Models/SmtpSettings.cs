namespace DataGuard.Core.Models;

/// <summary>
/// 이메일 발송용 SMTP 설정. 비밀번호는 여기 두지 않고
/// <see cref="CredentialKey"/>로 자격 증명 저장소에 보관한다(PRD 위험 ①).
/// </summary>
public sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool UseSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    /// <summary>발신자 주소(예: dataguard@company.com).</summary>
    public string FromAddress { get; set; } = string.Empty;

    public const string CredentialKey = "DataGuard:smtp";
}
