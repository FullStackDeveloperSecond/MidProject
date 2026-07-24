namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public List<string> Tags { get; set; } = new();
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public string? UploaderName { get; set; }
}
