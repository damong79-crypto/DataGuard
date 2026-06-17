using System.Collections.Concurrent;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// 트레이 상주 중 동작하는 인메모리 스케줄러(PRD 기능 ⑤, 상주형 A).
///
/// 현재는 골격만 제공한다 — 스케줄 문자열(cron/간격) 파싱과 다음 실행 시각 계산은
/// MVP 이후 단계에서 구현한다. 등록/해제/중지의 수명주기 관리 틀만 잡아둔다.
/// </summary>
public sealed class InAppCheckScheduler : ICheckScheduler, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();

    public void Schedule(CheckQuery query, Func<CheckQuery, Task> onTrigger)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(onTrigger);

        // 기존 등록이 있으면 교체.
        Unschedule(query.Id);

        // TODO(MVP 이후): query.Schedule 문자열을 파싱해 실제 주기/시각을 계산한다.
        // 지금은 스케줄 미구현 상태이므로 등록만 받아두고 타이머는 비활성으로 생성한다.
        var timer = new Timer(
            callback: _ => _ = onTrigger(query),
            state: null,
            dueTime: Timeout.Infinite,
            period: Timeout.Infinite);

        _timers[query.Id] = timer;
    }

    public void Unschedule(Guid queryId)
    {
        if (_timers.TryRemove(queryId, out Timer? timer))
        {
            timer.Dispose();
        }
    }

    public void StopAll()
    {
        foreach (Guid id in _timers.Keys)
        {
            Unschedule(id);
        }
    }

    public void Dispose() => StopAll();
}
