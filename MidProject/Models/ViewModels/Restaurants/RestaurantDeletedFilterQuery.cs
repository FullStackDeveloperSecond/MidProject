namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantDeletedFilterQuery
{
    public string? Search { get; set; }
    public string? City { get; set; }
    public string? Reason { get; set; }
    public int Page { get; set; } = 1;
}
