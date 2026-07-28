using MidProject.Models.ViewModels.PointsStore;

namespace MidProject.Services.IServices;

public interface IAvatarFrameService
{
    Task<AvatarFramesIndexViewModel> GetIndexAsync();
    Task<List<AvatarFrameDeletedRowViewModel>> GetDeletedIndexAsync();
    Task<AvatarFrameFormViewModel?> GetForEditAsync(int id);
    Task<(bool Success, string? Error)> CreateAsync(AvatarFrameFormViewModel form, int adminId);
    Task<(bool Success, string? Error)> UpdateAsync(int id, AvatarFrameFormViewModel form, int adminId);
    Task<bool> ToggleActiveAsync(int id);
    Task<bool> DeleteAsync(int id, int adminId);
    Task<bool> RestoreAsync(int id);
}
