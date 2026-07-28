using MidProject.Services.IServices;

namespace Member.Tests;

// ITaipeiClock 的假物件：測試不應該依賴「現在幾點」，用固定時間才能穩定斷言結果
// （例如懲罰結束時間 = 固定的現在時間 + 7 天）。
public sealed class FakeTaipeiClock : ITaipeiClock
{
    public DateTime Now { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

    public DateTime GetNow() => Now;

    public DateTime GetCurrentMinute() => NormalizeMinute(Now);

    public DateTime NormalizeMinute(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);
}
