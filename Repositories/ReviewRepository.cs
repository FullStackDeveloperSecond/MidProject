using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Repositories.IRepositories;

namespace MidProject.Repositories
{
    public class ReviewRepository : IReviewRepository
    {
        private readonly AppDbContext _db;

        public ReviewRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<(List<Review> Items, int TotalCount, int Page)> GetFilteredReviewsAsync(
            string tab, string? search, int? rating, string time, int page, int pageSize)
        {
            var query = BuildFilteredQuery(tab, search, rating, time);

            var total = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total, page);
        }

        public async Task<int> CountByStatusAsync(bool isDeleted, string? status)
        {
            if (isDeleted)
            {
                return await _db.Reviews.CountAsync(r => r.IsDeleted);
            }
            return await _db.Reviews.CountAsync(r => !r.IsDeleted && r.Status == status);
        }

        public async Task<Review?> GetByIdAsync(int id)
        {
            return await _db.Reviews.FindAsync(id);
        }

        public async Task<Review?> GetByIdWithDetailsAsync(int id)
        {
            return await _db.Reviews
                .Include(r => r.Member)
                .Include(r => r.Restaurant)
                .Include(r => r.DeletedByMember)
                .Include(r => r.ReviewImages)
                    .ThenInclude(ri => ri.Image)
                .FirstOrDefaultAsync(r => r.ReviewID == id);
        }

        public async Task<List<Report>> GetReportsForReviewAsync(int reviewId)
        {
            return await _db.Reports
                .Include(rp => rp.ReporterMember)
                .Where(rp => rp.ReviewID == reviewId && !rp.IsDeleted)
                .OrderByDescending(rp => rp.CreatedAt)
                .ToListAsync();
        }

        public async Task<Image?> GetImageByIdAsync(int imageId)
        {
            return await _db.Images.FindAsync(imageId);
        }

        public async Task<Restaurant?> GetRestaurantByIdAsync(int restaurantId)
        {
            return await _db.Restaurants.FindAsync(restaurantId);
        }

        public async Task<List<Review>> GetActiveReviewsForRestaurantAsync(int restaurantId)
        {
            return await _db.Reviews
                .Where(r => r.RestaurantID == restaurantId && !r.IsDeleted && r.Status == "Active")
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }

        /// <summary>對應規格書 4.2：全部要排除 IsDeleted = true，其餘依 Tab / 星等 / 時間 / 搜尋疊加篩選條件。</summary>
        private IQueryable<Review> BuildFilteredQuery(string tab, string? search, int? rating, string time)
        {
            var query = _db.Reviews
                .Include(r => r.Member)
                .Include(r => r.Restaurant)
                .AsQueryable();

            query = tab switch
            {
                "normal" => query.Where(r => !r.IsDeleted && r.Status == "Active"),
                "pending" => query.Where(r => !r.IsDeleted && r.Status == "PendingReview"),
                "deleted" => query.Where(r => r.IsDeleted),
                _ => query.Where(r => !r.IsDeleted), // "all"
            };

            if (rating.HasValue && rating.Value > 0)
            {
                query = query.Where(r => r.Rating == rating.Value);
            }

            if (time != "all")
            {
                var days = time == "7" ? 7 : 30;
                var cutoff = DateTime.Now.AddDays(-days);
                query = query.Where(r => r.CreatedAt >= cutoff);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    (r.Content != null && r.Content.Contains(search)) ||
                    (r.Member!.NickName != null && r.Member.NickName.Contains(search)) ||
                    r.Restaurant!.Name.Contains(search));
            }

            return query;
        }
    }
}
