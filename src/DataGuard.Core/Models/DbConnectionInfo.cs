using System.Text.Json.Serialization;

namespace DataGuard.Core.Models;

/// <summary>
/// 체크 대상 DB의 접속 정보.
/// 비밀번호는 이 모델에 보관하지 않는다 — 평문 노출 방지(PRD 4단계 / 위험 ①).
/// 실제 비밀번호는 <see cref="CredentialKey"/>를 키로 <c>ICredentialStore</c>에 별도 저장한다.
/// </summary>
public sealed class DbConnectionInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>사용자가 식별하는 연결 이름(예: "운영-주문DB").</summary>
    public string Name { get; set; } = string.Empty;

    public DbProvider Provider { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Database { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    /// <summary>자격 증명 저장소에서 비밀번호를 찾을 때 쓰는 키. 파생값이므로 직렬화 제외.</summary>
    [JsonIgnore]
    public string CredentialKey => $"DataGuard:db:{Id:N}";
}
