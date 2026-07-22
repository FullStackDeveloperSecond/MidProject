namespace MidProject.Services.IServices;

public interface ITaipeiClock
{
    DateTime GetNow();
    DateTime GetCurrentMinute();
    DateTime NormalizeMinute(DateTime value);
}
