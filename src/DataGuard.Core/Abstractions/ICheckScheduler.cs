using DataGuard.Core.Models;

namespace DataGuard.Core.Abstractions;

/// <summary>
/// 트레이 상주 중 쿼리를 스케줄대로 실행시킨다(PRD 기능 ⑤, 상주형 A).
/// 앱이 꺼져 있는 시간대의 체크는 보장하지 않는다(PRD 위험 ②).
/// </summary>
public interface ICheckScheduler
{
    /// <summary>쿼리를 스케줄에 등록한다. 실행 시점에 <paramref name="onTrigger"/>가 호출된다.</summary>
    void Schedule(CheckQuery query, Func<CheckQuery, Task> onTrigger);

    void Unschedule(Guid queryId);

    /// <summary>모든 스케줄을 중지(앱 종료 시).</summary>
    void StopAll();
}
