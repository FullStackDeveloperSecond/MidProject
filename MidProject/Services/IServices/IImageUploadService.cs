using Microsoft.AspNetCore.Http;
using MidProject.Models;

namespace MidProject.Services.IServices;

public interface IImageUploadService
{
    Task<Image> SaveAsync(
        IFormFile file,
        string imageType,
        int uploadedByMemberId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string? imageUrl,
        CancellationToken cancellationToken = default);
}
