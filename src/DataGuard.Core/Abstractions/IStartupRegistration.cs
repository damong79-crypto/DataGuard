namespace DataGuard.Core.Abstractions;

/// <summary>Windows 로그인 시 앱 자동 실행 등록을 관리한다(현재 사용자 범위).</summary>
public interface IStartupRegistration
{
    bool IsEnabled();

    void Enable();

    void Disable();
}
