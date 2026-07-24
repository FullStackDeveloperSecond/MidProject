using MidProject.Models.ViewModels;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IReviewRepository _repo;
        private const int PageSize = 10;

        public ReviewService(IReviewRepository repo)
        {
            _repo = repo;
        }

        public async Task<ReviewListViewModel> GetReviewListAsync(string tab, string? search, int? rating, string time, string sortBy, string sortDir, int? restaurantId, int page)
        {
            var (items, total, actualPage) = await _repo.GetFilteredReviewsAsync(tab, search, rating, time, sortBy, sortDir, restaurantId, page, PageSize);
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));

            return new ReviewListViewModel
            {
                Items = items,
                Tab = tab,
                Search = search,
                Rating = rating,
                Time = time,
                SortBy = sortBy == "rating" || sortBy == "report" ? sortBy : "time",
                SortDir = sortDir == "asc" ? "asc" : "desc",
                RestaurantID = restaurantId,
                Page = actualPage,
                PageSize = PageSize,
                TotalPages = totalPages,
                TotalCount = total,

                // 三格統計卡片 — 對應規格書 4.1，跟目前的篩選條件無關，永遠統計全部
                NormalCount = await _repo.CountByStatusAsync(false, "Active"),
                PendingCount = await _repo.CountByStatusAsync(false, "PendingReview"),
                DeletedCount = await _repo.CountByStatusAsync(true, null),
            };
        }

        public async Task<ReviewDetailViewModel?> GetReviewDetailAsync(int id)
        {
            var review = await _repo.GetByIdWithDetailsAsync(id);
            if (review == null)
            {
                return null;
            }

            var reports = await _repo.GetReportsForReviewAsync(id);

            return new ReviewDetailViewModel
            {
                Review = review,
                Reports = reports,
            };
        }

        public async Task<bool> SoftDeleteAsync(int id, int adminMemberId)
        {
            var review = await _repo.GetByIdAsync(id);
            if (review == null)
            {
                return false;
            }

            review.IsDeleted = true;
            review.DeletedAt = DateTime.Now;
            review.DeletedBy = adminMemberId;
            review.UpdatedAt = DateTime.Now;
            await _repo.SaveChangesAsync();

            // 規格書第 7 節：軟刪除後要重算餐廳的 AverageRating / ReviewCount
            await RecalculateRestaurantStatsAsync(review.RestaurantID);
            return true;
        }

        public async Task<bool> RestoreAsync(int id)
        {
            var review = await _repo.GetByIdAsync(id);
            if (review == null)
            {
                return false;
            }

            review.IsDeleted = false;
            review.DeletedAt = null;
            review.DeletedBy = null;
            review.UpdatedAt = DateTime.Now;
            await _repo.SaveChangesAsync();

            await RecalculateRestaurantStatsAsync(review.RestaurantID);
            return true;
        }

        public async Task<bool> DeleteImageAsync(int imageId, int adminMemberId)
        {
            var image = await _repo.GetImageByIdAsync(imageId);
            if (image == null)
            {
                return false;
            }

            // 規格書 5.5 / 8：只做軟刪除，不刪實體檔
            image.IsDeleted = true;
            image.DeletedAt = DateTime.Now;
            image.DeletedBy = adminMemberId;
            await _repo.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// 規格書第 7 節重算規則：只統計 IsDeleted = false 且 Status = Active 的評論。
        /// 沒有評論時 AverageRating = 0、ReviewCount = 0。
        /// </summary>
        private async Task RecalculateRestaurantStatsAsync(int restaurantId)
        {
            var activeReviews = await _repo.GetActiveReviewsForRestaurantAsync(restaurantId);
            var restaurant = await _repo.GetRestaurantByIdAsync(restaurantId);
            if (restaurant == null)
            {
                return;
            }

            restaurant.ReviewCount = activeReviews.Count;
            restaurant.AverageRating = activeReviews.Count > 0
                ? Math.Round((decimal)activeReviews.Average(r => r.Rating), 2)
                : 0m;
            restaurant.UpdatedAt = DateTime.Now;

            await _repo.SaveChangesAsync();
        }
    }
}
