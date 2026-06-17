using System.Collections.Concurrent;
using DataGuard.Core.Abstractions;
using DataGuard.Core.Models;

namespace DataGuard.Core.Services;

/// <summary>
/// 트레이 상주 중 동작하는 인메모리 스케줄러(PRD 기능 ⑤, 상주형 A).
/// 각 쿼리마다 "다음 실행 시각"을 계산해 일회성 타이머를 걸고, 실행 후 다음 회차를 다시 건다.
/// 앱이 꺼져 있는 시간대의 체크는 보장하지 않는다(PRD 위험 ②).
/// </summary>
public sealed class InAppCheckScheduler : ICheckScheduler, IDisposable
{
    // .NET Timer의 최대 지연(약 24.8일). 우리 스케줄(분/일/평일)은 항상 이보다 짧다.
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMilliseconds(int.MaxValue - 1);

    private readonly ConcurrentDictionary<Guid, Timer> _timers = new();
    private bool _disposed;

    public void Schedule(CheckQuery query, Func<CheckQuery, Task> onTrigger)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(onTrigger);

        Unschedule(query.Id);

        // 스케줄이 없거나 비활성 쿼리는 자동 실행 대상이 아니다.
        if (query.Schedule is null || !query.IsEnabled || _disposed)
        {
            return;
        }

        ArmNext(query, onTrigger);
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

    // 다음 실행 시각까지의 일회성 타이머를 건다. 실행 후 다음 회차를 위해 스스로 다시 무장한다.
    private void ArmNext(CheckQuery query, Func<CheckQuery, Task> onTrigger)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DateTimeOffset? next = query.Schedule!.GetNextRun(now);
        if (next is null || _disposed)
        {
            return;
        }

        TimeSpan delay = next.Value - now;
        if (delay < TimeSpan.Zero)
        {
            delay = TimeSpan.Zero;
        }
        else if (delay > MaxDelay)
        {
            delay = MaxDelay;
        }

        var timer = new Timer(
            callback: async void (_) =>
            {
                try
                {
                    await onTrigger(query).ConfigureAwait(false);
                }
                catch
                {
                    // 트리거 콜백은 자체적으로 예외를 처리해야 한다.
                    // 여기서는 스케줄러 스레드가 죽지 않도록 최후 방어만 한다.
                }
                finally
                {
                    ArmNext(query, onTrigger); // 다음 회차 예약
                }
            },
            state: null,
            dueTime: delay,
            period: Timeout.InfiniteTimeSpan);

        // 방금 발화한(또는 교체될) 이전 타이머를 정리하고 새 타이머로 교체.
        if (_timers.TryRemove(query.Id, out Timer? previous))
        {
            previous.Dispose();
        }
        _timers[query.Id] = timer;
    }

    public void Dispose()
    {
        _disposed = true;
        StopAll();
    }
}
