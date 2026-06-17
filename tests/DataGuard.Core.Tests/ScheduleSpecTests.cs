using DataGuard.Core.Models;

namespace DataGuard.Core.Tests;

public class ScheduleSpecTests
{
    // 모든 테스트는 KST(+09:00) 고정 오프셋을 사용한다. GetNextRun은 from.Offset을 그대로 따른다.
    private static readonly TimeSpan Kst = TimeSpan.FromHours(9);

    private static DateTimeOffset At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, Kst);

    // --- EveryMinutes ---

    [Fact]
    public void EveryMinutes_ReturnsNowPlusInterval()
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.EveryMinutes, IntervalMinutes = 60 };
        var from = At(2026, 6, 17, 10, 0);

        Assert.Equal(At(2026, 6, 17, 11, 0), spec.GetNextRun(from));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void EveryMinutes_NonPositiveInterval_ReturnsNull(int minutes)
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.EveryMinutes, IntervalMinutes = minutes };

        Assert.Null(spec.GetNextRun(At(2026, 6, 17, 10, 0)));
    }

    // --- DailyAt ---

    [Fact]
    public void DailyAt_TimeNotYetPassed_ReturnsToday()
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.DailyAt, TimeOfDay = new TimeSpan(9, 0, 0) };
        var from = At(2026, 6, 17, 8, 0); // 09:00 이전

        Assert.Equal(At(2026, 6, 17, 9, 0), spec.GetNextRun(from));
    }

    [Fact]
    public void DailyAt_TimeAlreadyPassed_ReturnsTomorrow()
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.DailyAt, TimeOfDay = new TimeSpan(9, 0, 0) };
        var from = At(2026, 6, 17, 10, 0); // 09:00 지남

        Assert.Equal(At(2026, 6, 18, 9, 0), spec.GetNextRun(from));
    }

    // --- WeekdaysAt ---
    // 달력 기준: 2026-06-17=수, 06-19=금, 06-20=토, 06-22=월

    [Fact]
    public void WeekdaysAt_WeekdayBeforeTime_ReturnsSameDay()
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.WeekdaysAt, TimeOfDay = new TimeSpan(9, 0, 0) };
        var from = At(2026, 6, 17, 8, 0); // 수요일 08:00

        Assert.Equal(At(2026, 6, 17, 9, 0), spec.GetNextRun(from));
    }

    [Fact]
    public void WeekdaysAt_FridayAfterTime_SkipsWeekendToMonday()
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.WeekdaysAt, TimeOfDay = new TimeSpan(9, 0, 0) };
        var from = At(2026, 6, 19, 10, 0); // 금요일 10:00 (09:00 지남)

        Assert.Equal(At(2026, 6, 22, 9, 0), spec.GetNextRun(from)); // 월요일
    }

    [Fact]
    public void WeekdaysAt_Saturday_SkipsToMonday()
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.WeekdaysAt, TimeOfDay = new TimeSpan(9, 0, 0) };
        var from = At(2026, 6, 20, 8, 0); // 토요일 08:00

        Assert.Equal(At(2026, 6, 22, 9, 0), spec.GetNextRun(from)); // 월요일
    }

    [Fact]
    public void NextRun_IsAlwaysAfterFrom()
    {
        var spec = new ScheduleSpec { Kind = ScheduleKind.WeekdaysAt, TimeOfDay = new TimeSpan(9, 0, 0) };
        var from = At(2026, 6, 17, 9, 0); // 정확히 시각과 동일 → 다음 회차로 가야 함

        DateTimeOffset? next = spec.GetNextRun(from);

        Assert.NotNull(next);
        Assert.True(next > from);
    }
}
