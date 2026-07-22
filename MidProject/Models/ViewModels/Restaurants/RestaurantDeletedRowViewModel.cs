namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantDeletedRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public DateTime? DeletedAt { get; set; }
    public string? DeleteReason { get; set; }
    public string? DeletedByName { get; set; }
}
