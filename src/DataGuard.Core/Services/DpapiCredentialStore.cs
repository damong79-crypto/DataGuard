using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using DataGuard.Core.Abstractions;

namespace DataGuard.Core.Services;

/// <summary>
/// Windows DPAPI(현재 사용자 범위)로 비밀을 암호화해 로컬 파일에 저장한다.
/// 같은 Windows 사용자 계정만 복호화 가능 → 평문 저장 방지(PRD 위험 ①).
///
/// 대안: Windows 자격 증명 관리자(Credential Manager) 직접 사용 — OS UI에서 관리 가능하나
/// P/Invoke가 필요. DPAPI 파일 방식은 의존성이 가벼워 MVP에 채택.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore : ICredentialStore
{
    // 복호화 난이도를 약간 높이는 추가 엔트로피(앱 고정 값). 키 자체는 사용자 DPAPI가 보호.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DataGuard.v1");

    private readonly string _directory;

    public DpapiCredentialStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DataGuard",
            "credentials");
        Directory.CreateDirectory(_directory);
    }

    public void Save(string key, string secret)
    {
        byte[] encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(PathFor(key), encrypted);
    }

    public string? Retrieve(string key)
    {
        string path = PathFor(key);
        if (!File.Exists(path))
        {
            return null;
        }

        byte[] decrypted = ProtectedData.Unprotect(
            File.ReadAllBytes(path), Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decrypted);
    }

    public void Delete(string key)
    {
        string path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    // 키에 경로 구분자 등이 섞여도 안전하도록 파일명으로 안전 인코딩한다.
    private string PathFor(string key)
    {
        string safe = Convert.ToHexString(Encoding.UTF8.GetBytes(key));
        return Path.Combine(_directory, $"{safe}.bin");
    }
}
