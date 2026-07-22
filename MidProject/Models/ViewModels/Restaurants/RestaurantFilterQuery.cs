namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantFilterQuery
{
    public string? Search { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public int? TagId { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
}
