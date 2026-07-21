namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantDeletedIndexViewModel
{
    public int StatsDisabledTotal { get; set; }
    public string? StatsMostRecentName { get; set; }
    public DateTime? StatsMostRecentAt { get; set; }
    public string? StatsLastReason { get; set; }

    public List<RestaurantDeletedRowViewModel> Items { get; set; } = new();

    public RestaurantDeletedFilterQuery Filter { get; set; } = new();

    public List<string> AvailableCities { get; set; } = new();
    public List<string> AvailableReasons { get; set; } = new();
}
