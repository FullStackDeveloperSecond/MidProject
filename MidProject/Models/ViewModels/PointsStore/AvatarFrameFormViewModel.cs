using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MidProject.Models.ViewModels.PointsStore;

public class AvatarFrameFormViewModel
{
    public int? FrameID { get; set; }

    [Required(ErrorMessage = "請輸入商品名稱"), StringLength(50)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "商品描述最多 500 字")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "請選擇分類")]
    [RegularExpression("^(Common|Rare|Limited)$", ErrorMessage = "分類只能是 Common、Rare 或 Limited")]
    public string Rarity { get; set; } = "Common";

    [Range(0, int.MaxValue, ErrorMessage = "點數售價不可為負數")]
    public int PointsPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public int? ExistingImageId { get; set; }
    public string? ExistingImageUrl { get; set; }
    public IFormFile? ImageFile { get; set; }

    public bool IsEdit => FrameID.HasValue;
}
