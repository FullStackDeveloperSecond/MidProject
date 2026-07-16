namespace MidProject.Models.ViewModels.Restaurants;

public class BusinessHourGroupViewModel
{
    public int DayOfWeek { get; set; }
    public string DayLabel { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public List<TimeSlotRow> Slots { get; set; } = new();
}
