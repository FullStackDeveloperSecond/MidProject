namespace MidProject.Models;

public class BusinessHour
{
    public int BusinessHourID { get; set; }
    public int RestaurantID { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; }

    public Restaurant? Restaurant { get; set; }
}
