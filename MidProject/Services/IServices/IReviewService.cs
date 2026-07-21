using MidProject.Models.ViewModels;

namespace MidProject.Services.IServices
{

    public interface IReviewService
    {
        Task<ReviewListViewModel> GetReviewListAsync(string tab, string? search, int? rating, string time, int page);

        /// <summary>找不到該評論時回傳 null，Controller 負責轉成 404。</summary>
        Task<ReviewDetailViewModel?> GetReviewDetailAsync(int id);

        /// <summary>成功回傳 true；評論不存在回傳 false。</summary>
        Task<bool> SoftDeleteAsync(int id, int adminMemberId);

        Task<bool> RestoreAsync(int id);

        Task<bool> DeleteImageAsync(int imageId, int adminMemberId);
    }
}
