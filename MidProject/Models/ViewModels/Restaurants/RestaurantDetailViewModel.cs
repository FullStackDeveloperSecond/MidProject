namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantDetailViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string DetailedAddress { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Note { get; set; }
    public List<TagOptionViewModel> Tags { get; set; } = new();
    public string OwnerName { get; set; } = string.Empty;
    public int OwnerMemberId { get; set; }

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }

    public List<BusinessHourGroupViewModel> HoursByDay { get; set; } = new();

    public string? CoverImageUrl { get; set; }
    public List<string> EnvironmentImageUrls { get; set; } = new();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByName { get; set; }
    public string? DeleteReason { get; set; }
}
