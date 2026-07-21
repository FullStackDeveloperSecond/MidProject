using Microsoft.AspNetCore.Http;
using MidProject.Models;
using MidProject.Services.IServices;

namespace MidProject.Services;

/// <summary>
/// Placeholder MVP file upload — no crop/resize/WebP conversion.
/// Meant to be replaced by 愷/Alex's official image pipeline later.
/// </summary>
public class ImageUploadService : IImageUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private const long MaxBytes = 5 * 1024 * 1024;

    private readonly IWebHostEnvironment _env;

    public ImageUploadService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public async Task<Image> SaveAsync(IFormFile file, string imageType, int uploadedByMemberId)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidOperationException("請選擇圖片檔案。");
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("圖片僅接受 JPG / PNG / WebP 格式。");
        }

        if (file.Length > MaxBytes)
        {
            throw new InvalidOperationException("圖片檔案不可超過 5MB。");
        }

        var folder = Path.Combine(_env.WebRootPath, "uploads", imageType);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(folder, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return new Image
        {
            UploadedByMemberID = uploadedByMemberId,
            ImageURL = $"/uploads/{imageType}/{fileName}",
            ImageType = imageType,
            SortOrder = 0,
            UploadedAt = DateTime.Now
        };
    }
}
