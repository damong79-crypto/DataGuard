namespace DataGuard.Core.Abstractions;

/// <summary>
/// 비밀번호 등 민감 자격 증명을 안전하게 저장·조회한다.
/// 평문 저장 금지(PRD 위험 ①). 기본 구현은 Windows DPAPI(현재 사용자 범위).
/// </summary>
public interface ICredentialStore
{
    void Save(string key, string secret);

    /// <returns>저장된 비밀, 없으면 null.</returns>
    string? Retrieve(string key);

    void Delete(string key);
}
