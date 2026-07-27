using MidProject.Services.IServices;

namespace Reviews.Tests;

/// <summary>固定時間的假時鐘，讓測試的時間相關斷言是可預測的，不用真的等或用 DateTime.Now 猜。</summary>
public class FakeTaipeiClock : ITaipeiClock
{
    public DateTime Now { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0);

    public DateTime GetNow() => Now;

    public DateTime GetCurrentMinute() => new DateTime(Now.Year, Now.Month, Now.Day, Now.Hour, Now.Minute, 0);

    public DateTime NormalizeMinute(DateTime value) => new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);
}
