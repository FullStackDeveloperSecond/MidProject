using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class TaipeiClock : ITaipeiClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public TaipeiClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _timeZone = ResolveTimeZone();
    }

    public DateTime GetNow()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, _timeZone);
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public DateTime GetCurrentMinute() => NormalizeMinute(GetNow());

    public DateTime NormalizeMinute(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, DateTimeKind.Unspecified);

    private static TimeZoneInfo ResolveTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
        }
    }
}
