using MidProject.Models;

namespace MidProject.Repositories.IRepositories
{
    /// <summary>
    /// 評論模組的資料存取介面。
    /// 只負責「怎麼從資料庫撈資料 / 存資料」，不放商業邏輯（分頁換算、統計彙整、
    /// 重算餐廳分數等邏輯放在 Services/ReviewService.cs）。
    /// </summary>
    public interface IReviewRepository
    {
        /// <summary>依 Tab / 搜尋 / 星等 / 時間篩選並分頁，回傳這一頁的資料、總筆數，以及夾在合法範圍內的頁碼。
        /// sortBy 傳 "time" / "rating" / "report"，sortDir 傳 "asc" 或 "desc"。</summary>
        Task<(List<Review> Items, int TotalCount, int Page)> GetFilteredReviewsAsync(
            string tab, string? search, int? rating, string time, string sortBy, string sortDir, int page, int pageSize, int? restaurantId = null);

        /// <summary>依狀態算數量。isDeleted=true 時忽略 status，直接算所有已刪除的。</summary>
        Task<int> CountByStatusAsync(bool isDeleted, string? status);

        /// <summary>單純用主鍵撈評論（不帶關聯資料），給軟刪除／還原這種只需要改欄位的場景用。</summary>
        Task<Review?> GetByIdAsync(int id);

        /// <summary>撈評論詳情頁需要的完整資料（會員、餐廳、刪除者、圖片）。</summary>
        Task<Review?> GetByIdWithDetailsAsync(int id);

        /// <summary>撈某則評論底下所有未刪除的檢舉紀錄。</summary>
        Task<List<Report>> GetReportsForReviewAsync(int reviewId);

        /// <summary>取得確實隸屬指定評論的評論圖片；不允許跨評論或跨模組操作圖片。</summary>
        Task<Image?> GetImageForReviewAsync(int imageId, int reviewId);

        Task<Restaurant?> GetRestaurantByIdAsync(int restaurantId);

        /// <summary>撈某餐廳目前「正常且未刪除」的評論，用來重算 AverageRating / ReviewCount。</summary>
        Task<List<Review>> GetActiveReviewsForRestaurantAsync(int restaurantId);

        Task SaveChangesAsync();
    }
}
