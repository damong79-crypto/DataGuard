using DataGuard.Core.Models;

namespace DataGuard.Core.Abstractions;

/// <summary>앱 설정(연결·쿼리·SMTP·수신자)을 영속화한다.</summary>
public interface IAppConfigStore
{
    /// <summary>저장된 설정을 읽는다. 파일이 없으면 빈 기본 설정을 반환한다.</summary>
    AppConfig Load();

    void Save(AppConfig config);
}
