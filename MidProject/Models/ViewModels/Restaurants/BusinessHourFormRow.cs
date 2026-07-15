namespace MidProject.Models.ViewModels.Restaurants;

public class BusinessHourFormRow
{
    public int DayOfWeek { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public List<TimeSlotRow> Slots { get; set; } = new();
}
