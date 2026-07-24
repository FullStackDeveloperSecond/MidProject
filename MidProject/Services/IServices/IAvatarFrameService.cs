using MidProject.Models.ViewModels.PointsStore;

namespace MidProject.Services.IServices;

public interface IAvatarFrameService
{
    Task<AvatarFramesIndexViewModel> GetIndexAsync(string? keyword, string? rarity, bool? isActive, string? sortBy, int page);
    Task<AvatarFramesDeletedIndexViewModel> GetDeletedIndexAsync(string? keyword, string? rarity, string? sortBy, int page);
    Task<AvatarFrameFormViewModel?> GetForEditAsync(int id);
    Task<(bool Success, string? Error)> CreateAsync(AvatarFrameFormViewModel form, int adminId);
    Task<(bool Success, string? Error)> UpdateAsync(int id, AvatarFrameFormViewModel form, int adminId);
    Task ToggleActiveAsync(int id);
    Task DeleteAsync(int id, int adminId);
    Task RestoreAsync(int id);
}
