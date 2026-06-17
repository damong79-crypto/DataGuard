namespace DataGuard.Core.Models;

/// <summary>
/// 앱 전체 설정. 비밀번호 등 민감 정보는 여기 두지 않고 자격 증명 저장소에 보관한다(PRD 위험 ①).
/// JSON 파일로 영속화된다.
/// </summary>
public sealed class AppConfig
{
    public List<DbConnectionInfo> Connections { get; set; } = new();

    public List<CheckQuery> Queries { get; set; } = new();

    public SmtpSettings Smtp { get; set; } = new();

    /// <summary>결과를 받을 수신자 이메일 목록(PRD 2단계: 수신자).</summary>
    public List<string> Recipients { get; set; } = new();

    /// <summary>이력 보관 기간(일). 이보다 오래된 이력은 자동 삭제. 0이면 무제한(정리 안 함).</summary>
    public int HistoryRetentionDays { get; set; } = 90;

    /// <summary>SMTP 호스트가 비어 있으면 이메일 발송을 건너뛴다.</summary>
    public bool IsEmailConfigured =>
        !string.IsNullOrWhiteSpace(Smtp.Host) && Recipients.Count > 0;
}
