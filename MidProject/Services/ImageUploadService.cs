using MidProject.Services.IServices;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using EntityImage = MidProject.Models.Image;
using ImageSharpImage = SixLabors.ImageSharp.Image;

namespace MidProject.Services;

public class ImageUploadService : IImageUploadService
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> AllowedFormatExtensions =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["JPEG"] = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" },
            ["PNG"] = new(StringComparer.OrdinalIgnoreCase) { ".png" },
            ["WEBP"] = new(StringComparer.OrdinalIgnoreCase) { ".webp" }
        };

    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.Ordinal)
    {
        "RestaurantCover",
        "RestaurantEnvironment",
        "ReviewImage",
        "MemberAvatar",
        "AvatarFrame"
    };

    private const long MaxBytes = 5 * 1024 * 1024;
    private const int MaxDimension = 4096;

    private readonly IWebHostEnvironment _env;
    private readonly ITaipeiClock _clock;
    private readonly ILogger<ImageUploadService> _logger;

    public ImageUploadService(
        IWebHostEnvironment env,
        ITaipeiClock clock,
        ILogger<ImageUploadService> logger)
    {
        _env = env;
        _clock = clock;
        _logger = logger;
    }

    public async Task<EntityImage> SaveAsync(
        IFormFile file,
        string imageType,
        int uploadedByMemberId,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new InvalidOperationException("請選擇圖片檔案。");
        }

        if (!AllowedImageTypes.Contains(imageType))
        {
            throw new InvalidOperationException("圖片用途不在允許清單中。");
        }

        var extension = Path.GetExtension(file.FileName);
        if (file.Length > MaxBytes)
        {
            throw new InvalidOperationException("圖片檔案不可超過 5MB。");
        }

        await using var input = file.OpenReadStream();
        SixLabors.ImageSharp.ImageInfo imageInfo;
        try
        {
            imageInfo = await ImageSharpImage.IdentifyAsync(input, cancellationToken);
        }
        catch (Exception exception) when (exception is
            SixLabors.ImageSharp.ImageFormatException or
            NotSupportedException)
        {
            throw new InvalidOperationException("圖片內容無法辨識或已損毀。", exception);
        }

        var detectedFormat = imageInfo.Metadata.DecodedImageFormat;
        if (detectedFormat is null ||
            !AllowedFormatExtensions.TryGetValue(detectedFormat.Name, out var validExtensions))
        {
            throw new InvalidOperationException("圖片僅接受 JPG / PNG / WebP 格式。");
        }

        if (!validExtensions.Contains(extension))
        {
            throw new InvalidOperationException("圖片副檔名與實際內容格式不一致。");
        }

        if (imageInfo.Width is <= 0 or > MaxDimension ||
            imageInfo.Height is <= 0 or > MaxDimension)
        {
            throw new InvalidOperationException($"圖片長寬不可超過 {MaxDimension}×{MaxDimension} 像素。");
        }

        if (imageInfo.FrameMetadataCollection.Count > 1)
        {
            throw new InvalidOperationException("僅接受單幀靜態圖片。");
        }

        input.Position = 0;
        SixLabors.ImageSharp.Image decodedImage;
        try
        {
            decodedImage = await ImageSharpImage.LoadAsync(input, cancellationToken);
        }
        catch (Exception exception) when (exception is
            SixLabors.ImageSharp.ImageFormatException or
            NotSupportedException)
        {
            throw new InvalidOperationException("圖片內容無法完整解析或已損毀。", exception);
        }

        using (decodedImage)
        {
            decodedImage.Mutate(context => context.AutoOrient());

            var normalizedExtension = detectedFormat.Name.ToUpperInvariant() switch
            {
                "JPEG" => ".jpg",
                "PNG" => ".png",
                "WEBP" => ".webp",
                _ => throw new InvalidOperationException("圖片格式不在允許清單中。")
            };

            var encoder = CreateEncoder(detectedFormat);
            var folder = Path.Combine(_env.WebRootPath, "uploads", imageType);
            Directory.CreateDirectory(folder);

            var fileName = $"{Guid.NewGuid():N}{normalizedExtension}";
            var fullPath = Path.Combine(folder, fileName);
            var temporaryPath = $"{fullPath}.tmp";

            try
            {
                await using (var output = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous))
                {
                    await decodedImage.SaveAsync(output, encoder, cancellationToken);
                }

                if (new FileInfo(temporaryPath).Length > MaxBytes)
                {
                    throw new InvalidOperationException("圖片重新處理後超過 5MB，請降低解析度或壓縮後再上傳。");
                }

                File.Move(temporaryPath, fullPath);
            }
            catch
            {
                TryDeleteFile(temporaryPath);
                TryDeleteFile(fullPath);
                throw;
            }

            return new EntityImage
            {
                UploadedByMemberID = uploadedByMemberId,
                ImageURL = $"/uploads/{imageType}/{fileName}",
                ImageType = imageType,
                SortOrder = 0,
                UploadedAt = _clock.GetNow()
            };
        }
    }

    public Task<bool> DeleteAsync(
        string? imageUrl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolveUploadPath(imageUrl, out var fullPath))
        {
            return Task.FromResult(false);
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return Task.FromResult(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(
                exception,
                "Image file cleanup failed at {TaipeiTimestamp}; Operation=DeleteImageFile; ImageUrl={ImageUrl}; ExceptionType={ExceptionType}",
                _clock.GetNow(),
                imageUrl,
                exception.GetType().Name);
            return Task.FromResult(false);
        }
    }

    private static ImageEncoder CreateEncoder(IImageFormat format) =>
        format.Name.ToUpperInvariant() switch
        {
            "JPEG" => new JpegEncoder { Quality = 90, SkipMetadata = true },
            "PNG" => new PngEncoder { SkipMetadata = true },
            "WEBP" => new WebpEncoder { Quality = 90, SkipMetadata = true },
            _ => throw new InvalidOperationException("圖片格式不在允許清單中。")
        };

    private bool TryResolveUploadPath(string? imageUrl, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return false;
        }

        var normalizedUrl = imageUrl.Replace('\\', '/');
        if (!normalizedUrl.StartsWith("/uploads/", StringComparison.Ordinal) ||
            normalizedUrl.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        var uploadRoot = Path.GetFullPath(Path.Combine(_env.WebRootPath, "uploads"));
        var relativePath = normalizedUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var candidatePath = Path.GetFullPath(Path.Combine(_env.WebRootPath, relativePath));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = uploadRoot.EndsWith(Path.DirectorySeparatorChar)
            ? uploadRoot
            : $"{uploadRoot}{Path.DirectorySeparatorChar}";

        if (!candidatePath.StartsWith(rootPrefix, comparison))
        {
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // The original save exception remains the actionable failure.
        }
    }
}
