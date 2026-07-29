using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models.ViewModels;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services
{
    public class ReviewService : IReviewService
    {
        private readonly IReviewRepository _repo;
        private readonly AppDbContext _dbContext;
        private readonly ITaipeiClock _clock;
        private readonly IImageLifecycleService _imageLifecycleService;
        private const int PageSize = 10;

        public ReviewService(
            IReviewRepository repo,
            AppDbContext dbContext,
            ITaipeiClock clock,
            IImageLifecycleService imageLifecycleService)
        {
            _repo = repo;
            _dbContext = dbContext;
            _clock = clock;
            _imageLifecycleService = imageLifecycleService;
        }

        public async Task<ReviewListViewModel> GetReviewListAsync(string tab, string? search, int? rating, string time, string sortBy, string sortDir, int page, int? restaurantId = null)
        {
            var (items, total, actualPage) = await _repo.GetFilteredReviewsAsync(tab, search, rating, time, sortBy, sortDir, page, PageSize, restaurantId);
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

            var now = _clock.GetNow();

            // 規格書第 7 節：軟刪除跟餐廳統計重算要一起成功或一起回滾，避免評論已標記刪除
            // 但 AverageRating / ReviewCount 沒同步更新的不一致狀態。
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                review.IsDeleted = true;
                review.DeletedAt = now;
                review.DeletedBy = adminMemberId;
                review.UpdatedAt = now;
                await _repo.SaveChangesAsync();

                await RecalculateRestaurantStatsAsync(review.RestaurantID);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return true;
        }

        /// <summary>
        /// 還原契約（T-R-05）：還原會把 DeletedAt / DeletedBy 清空，跟這則評論從未被刪除過一樣，
        /// 不會保留「是誰、何時把它還原」的紀錄——這是刻意的選擇，不是遺漏。如果之後真的需要
        /// 還原稽核（誰在什麼時候復原了這則評論），需要幫 Review 加 RestoredAt / RestoredBy 欄位，
        /// 這會動到共用的 AppDbContext / Migration，要跟 Alex／愷協調後再加。
        /// </summary>
        public async Task<bool> RestoreAsync(int id)
        {
            var review = await _repo.GetByIdAsync(id);
            if (review == null)
            {
                return false;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                review.IsDeleted = false;
                review.DeletedAt = null;
                review.DeletedBy = null;
                review.UpdatedAt = _clock.GetNow();
                await _repo.SaveChangesAsync();

                await RecalculateRestaurantStatsAsync(review.RestaurantID);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            return true;
        }

        public async Task<bool> DeleteImageAsync(int imageId, int reviewId, int adminMemberId)
        {
            var image = await _repo.GetImageForReviewAsync(imageId, reviewId);
            if (image == null)
            {
                return false;
            }

            var reviewLinks = await _dbContext.ReviewImages
                .Where(link => link.ReviewID == reviewId && link.ImageID == imageId)
                .ToListAsync();
            _dbContext.ReviewImages.RemoveRange(reviewLinks);

            image.IsDeleted = true;
            image.DeletedAt = _clock.GetNow();
            image.DeletedBy = adminMemberId;
            await _repo.SaveChangesAsync();

            await _imageLifecycleService.CleanupIfUnreferencedAsync([imageId], adminMemberId);
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
            restaurant.UpdatedAt = _clock.GetNow();

            await _repo.SaveChangesAsync();
        }
    }
}
