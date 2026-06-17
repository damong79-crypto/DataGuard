using System.Runtime.Versioning;
using DataGuard.Core.Abstractions;
using Microsoft.Win32;

namespace DataGuard.Core.Services;

/// <summary>
/// HKCU\...\Run 레지스트리로 자동 실행을 등록한다(현재 사용자 범위 → 관리자 권한 불필요).
/// 시작 시 트레이로 조용히 뜨도록 "--minimized" 인자를 함께 등록한다.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class StartupRegistration : IStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DataGuard";

    public bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public void Enable()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            throw new InvalidOperationException("실행 파일 경로를 확인할 수 없습니다.");
        }

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, $"\"{exePath}\" --minimized");
    }

    public void Disable()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
