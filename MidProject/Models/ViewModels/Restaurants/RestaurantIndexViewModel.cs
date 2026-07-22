namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantIndexViewModel
{
    public int StatsTotal { get; set; }
    public decimal StatsAvgRating { get; set; }
    public int StatsReviewCount { get; set; }
    public int StatsDisabledCount { get; set; }

    public List<RestaurantRowViewModel> Items { get; set; } = new();

    public RestaurantFilterQuery Filter { get; set; } = new();

    public List<string> AvailableCities { get; set; } = new();
    public List<string> AvailableDistricts { get; set; } = new();
    public List<(int Id, string Name)> AvailableTags { get; set; } = new();

    public int TotalItems { get; set; }
    public int PageSize { get; set; } = 10;
    public int CurrentPage { get; set; } = 1;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
}
