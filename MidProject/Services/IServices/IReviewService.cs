using MidProject.Models.ViewModels;

namespace MidProject.Services.IServices
{
    /// <summary>
    /// 評論模組的商業邏輯：組出 ViewModel、統計彙整、軟刪除／還原、
    /// 以及軟刪除／還原後重算餐廳分數。實際查資料庫的細節交給 IReviewRepository。
    /// </summary>
    public interface IReviewService
    {
        Task<ReviewListViewModel> GetReviewListAsync(string tab, string? search, int? rating, string time, string sortBy, string sortDir, int? restaurantId, int page);

        /// <summary>找不到該評論時回傳 null，Controller 負責轉成 404。</summary>
        Task<ReviewDetailViewModel?> GetReviewDetailAsync(int id);

        /// <summary>成功回傳 true；評論不存在回傳 false。</summary>
        Task<bool> SoftDeleteAsync(int id, int adminMemberId);

        Task<bool> RestoreAsync(int id);

        Task<bool> DeleteImageAsync(int imageId, int adminMemberId);
    }
}
