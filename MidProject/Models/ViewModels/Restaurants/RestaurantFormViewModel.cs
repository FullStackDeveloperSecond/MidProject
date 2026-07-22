using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MidProject.Models.ViewModels.Restaurants;

public class RestaurantFormViewModel
{
    public int? Id { get; set; }

    [Required(ErrorMessage = "請輸入餐廳名稱"), StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [Required(ErrorMessage = "請選擇縣市"), StringLength(20)]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "請選擇行政區"), StringLength(20)]
    public string District { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入詳細地址"), StringLength(200)]
    public string DetailedAddress { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "備註最多 1000 字")]
    public string? Note { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    public List<int> SelectedTagIds { get; set; } = new();
    public List<TagOptionViewModel> AvailableTags { get; set; } = new();

    public List<BusinessHourFormRow> Hours { get; set; } = new();

    /// <summary>
    /// JSON-encoded business hours built/read by wwwroot/js/restaurant-admin.js.
    /// Avoids fragile List&lt;T&gt; index rebinding when slots are added/removed client-side.
    /// </summary>
    public string? HoursJson { get; set; }

    public int? ExistingCoverImageId { get; set; }
    public string? ExistingCoverImageUrl { get; set; }
    public bool RemoveCoverImage { get; set; }
    public IFormFile? CoverImageFile { get; set; }

    public List<ExistingImageViewModel> ExistingEnvironmentImages { get; set; } = new();
    public List<int> RemoveEnvironmentImageIds { get; set; } = new();
    public List<IFormFile> EnvironmentImageFiles { get; set; } = new();

    public bool IsEdit => Id.HasValue;
}
