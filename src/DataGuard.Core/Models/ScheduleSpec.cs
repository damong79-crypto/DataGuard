namespace DataGuard.Core.Models;

/// <summary>스케줄 종류. cron 대신 PRD 예시(매시간·평일 09:00 등)를 직관적으로 표현한다.</summary>
public enum ScheduleKind
{
    /// <summary>지정 간격(분)마다 실행.</summary>
    EveryMinutes,

    /// <summary>매일 지정 시각에 실행.</summary>
    DailyAt,

    /// <summary>평일(월~금) 지정 시각에 실행.</summary>
    WeekdaysAt
}

/// <summary>
/// 쿼리 자동 실행 스케줄. 상주형(A) 전제이므로 앱이 켜져 있는 동안만 동작한다(PRD 위험 ②).
/// </summary>
public sealed class ScheduleSpec
{
    public ScheduleKind Kind { get; set; }

    /// <summary><see cref="ScheduleKind.EveryMinutes"/>일 때 사용하는 간격(분).</summary>
    public int IntervalMinutes { get; set; }

    /// <summary>DailyAt·WeekdaysAt에서 실행할 하루 중 시각.</summary>
    public TimeSpan TimeOfDay { get; set; }

    /// <summary>
    /// <paramref name="from"/> 이후의 다음 실행 시각을 계산한다. 계산 불가면 null.
    /// </summary>
    public DateTimeOffset? GetNextRun(DateTimeOffset from)
    {
        switch (Kind)
        {
            case ScheduleKind.EveryMinutes:
                return IntervalMinutes > 0 ? from.AddMinutes(IntervalMinutes) : null;

            case ScheduleKind.DailyAt:
            {
                DateTimeOffset candidate = AtTime(from, TimeOfDay);
                return candidate > from ? candidate : candidate.AddDays(1);
            }

            case ScheduleKind.WeekdaysAt:
            {
                DateTimeOffset candidate = AtTime(from, TimeOfDay);
                if (candidate <= from)
                {
                    candidate = candidate.AddDays(1);
                }

                // 토·일이면 다음 평일로 밀어낸다.
                while (candidate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    candidate = candidate.AddDays(1);
                }

                return candidate;
            }

            default:
                return null;
        }
    }

    private static DateTimeOffset AtTime(DateTimeOffset reference, TimeSpan timeOfDay) =>
        new(reference.Date.Add(timeOfDay), reference.Offset);

    public override string ToString() => Kind switch
    {
        ScheduleKind.EveryMinutes => $"매 {IntervalMinutes}분",
        ScheduleKind.DailyAt => $"매일 {TimeOfDay:hh\\:mm}",
        ScheduleKind.WeekdaysAt => $"평일 {TimeOfDay:hh\\:mm}",
        _ => "수동"
    };
}
