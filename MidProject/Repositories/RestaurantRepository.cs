using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels.Restaurants;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Repositories;

public class RestaurantRepository : IRestaurantRepository
{
    private readonly AppDbContext _db;
    private readonly ITaipeiClock _clock;

    public RestaurantRepository(AppDbContext db, ITaipeiClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // Index/Deleted rows only ever render Name/City/District/Phone/Tags/Rating/
    // ReviewCount (+ DeletedAt/DeletedByName for the deleted list) — never business
    // hours or images. The old shared BaseQuery() Include()'d BusinessHours,
    // RestaurantTags AND RestaurantImages together (three separate collections) in
    // one query; without AsSplitQuery() that becomes a single SQL statement with
    // multiple JOINs, and the row count returned is the *cartesian product* of all
    // three collections per restaurant (e.g. 8 business-hour rows × 2 tags × 1 image
    // = 16 duplicate rows fetched just to reconstruct one restaurant). That was the
    // main cause of the list page occasionally feeling slow to load. This lean query
    // only joins what the list actually shows.
    private IQueryable<Restaurant> ListQuery()
    {
        return _db.Restaurants
            .Include(r => r.Member)
            .Include(r => r.DeletedByMember)
            .Include(r => r.RestaurantTags).ThenInclude(rt => rt.Tag);
    }

    // The detail/edit view genuinely needs all of these. Multiple collection
    // Include()s still risk the same cartesian-product blow-up here, so this is
    // explicitly split into separate SQL queries (one per collection) instead of
    // one giant join — the officially recommended EF Core pattern for this shape.
    private IQueryable<Restaurant> DetailQuery()
    {
        return _db.Restaurants
            .Include(r => r.Member)
            .Include(r => r.DeletedByMember)
            .Include(r => r.BusinessHours)
            .Include(r => r.RestaurantTags).ThenInclude(rt => rt.Tag)
            .Include(r => r.RestaurantImages).ThenInclude(ri => ri.Image)
            .AsSplitQuery();
    }

    private static IQueryable<Restaurant> ApplyCommonFilters(IQueryable<Restaurant> query, string? search, string? city, string? district, int? tagId)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(r =>
                r.Name.Contains(keyword) ||
                r.DetailedAddress.Contains(keyword) ||
                (r.Note != null && r.Note.Contains(keyword)) ||
                (r.Member != null && (r.Member.UserName.Contains(keyword) || (r.Member.NickName != null && r.Member.NickName.Contains(keyword)))));
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            query = query.Where(r => r.City == city);
        }

        if (!string.IsNullOrWhiteSpace(district))
        {
            query = query.Where(r => r.District == district);
        }

        if (tagId.HasValue)
        {
            query = query.Where(r => r.RestaurantTags.Any(rt => rt.TagID == tagId.Value));
        }

        return query;
    }

    public async Task<(List<Restaurant> Items, int TotalCount)> GetActivePagedAsync(RestaurantFilterQuery filter, int pageSize)
    {
        var query = ApplyCommonFilters(ListQuery().Where(r => !r.IsDeleted), filter.Search, filter.City, filter.District, filter.TagId);

        query = filter.Sort switch
        {
            "rating" => query.OrderByDescending(r => r.AverageRating).ThenBy(r => r.RestaurantID),
            "review" => query.OrderByDescending(r => r.ReviewCount).ThenBy(r => r.RestaurantID),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.RestaurantID)
        };

        var totalCount = await query.CountAsync();
        var page = Math.Max(1, filter.Page);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return (items, totalCount);
    }

    private IQueryable<Restaurant> ApplyDeletedFilters(RestaurantDeletedFilterQuery filter)
    {
        var query = ApplyCommonFilters(ListQuery().Where(r => r.IsDeleted), filter.Search, filter.City, null, null);

        if (!string.IsNullOrWhiteSpace(filter.Reason))
        {
            query = query.Where(r => r.DeleteReason == filter.Reason);
        }

        return query;
    }

    public async Task<(List<Restaurant> Items, int TotalCount)> GetDeletedPagedAsync(RestaurantDeletedFilterQuery filter, int pageSize)
    {
        var query = ApplyDeletedFilters(filter)
            .OrderByDescending(r => r.DeletedAt ?? r.CreatedAt)
            .ThenBy(r => r.RestaurantID);

        var totalCount = await query.CountAsync();
        var page = Math.Max(1, filter.Page);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return (items, totalCount);
    }

    // 「最近停用」統計卡片要反映目前篩選條件下真正最新的一筆，跟目前瀏覽第幾頁無關，
    // 所以獨立查一次（不受 Skip/Take 影響），而不是直接拿分頁後那批資料的第一筆。
    public async Task<Restaurant?> GetMostRecentlyDeletedAsync(RestaurantDeletedFilterQuery filter)
    {
        return await ApplyDeletedFilters(filter)
            .OrderByDescending(r => r.DeletedAt ?? r.CreatedAt)
            .ThenBy(r => r.RestaurantID)
            .FirstOrDefaultAsync();
    }

    public async Task<Restaurant?> GetByIdAsync(int id)
    {
        return await DetailQuery().FirstOrDefaultAsync(r => r.RestaurantID == id);
    }

    public async Task<IReadOnlyDictionary<int, RestaurantReviewStats>> GetReviewStatsAsync(IEnumerable<int> restaurantIds)
    {
        var ids = restaurantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, RestaurantReviewStats>();
        }

        var stats = await _db.Reviews
            .Where(r => ids.Contains(r.RestaurantID) && !r.IsDeleted && r.Status == "Active")
            .GroupBy(r => r.RestaurantID)
            .Select(g => new
            {
                RestaurantID = g.Key,
                AverageRating = Math.Round(g.Average(r => (decimal)r.Rating), 2),
                ReviewCount = g.Count()
            })
            .ToListAsync();

        return stats.ToDictionary(
            x => x.RestaurantID,
            x => new RestaurantReviewStats { AverageRating = x.AverageRating, ReviewCount = x.ReviewCount });
    }

    public async Task<IReadOnlyDictionary<int, int>> GetFavoriteCountsAsync(IEnumerable<int> restaurantIds)
    {
        var ids = restaurantIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var counts = await _db.Favorites
            .Where(f => ids.Contains(f.RestaurantID) && !f.IsDeleted)
            .GroupBy(f => f.RestaurantID)
            .Select(g => new { RestaurantID = g.Key, Count = g.Count() })
            .ToListAsync();

        return counts.ToDictionary(x => x.RestaurantID, x => x.Count);
    }

    public async Task AddAsync(Restaurant restaurant)
    {
        _db.Restaurants.Add(restaurant);
        await _db.SaveChangesAsync();
    }

    public async Task ReplaceBusinessHoursAsync(int restaurantId, List<BusinessHour> hours)
    {
        var existing = await _db.BusinessHours.Where(h => h.RestaurantID == restaurantId).ToListAsync();
        _db.BusinessHours.RemoveRange(existing);

        foreach (var hour in hours)
        {
            hour.RestaurantID = restaurantId;
            _db.BusinessHours.Add(hour);
        }

        await _db.SaveChangesAsync();
    }

    public async Task ReplaceTagsAsync(int restaurantId, List<int> tagIds)
    {
        var existing = await _db.RestaurantTags.Where(rt => rt.RestaurantID == restaurantId).ToListAsync();
        _db.RestaurantTags.RemoveRange(existing);

        foreach (var tagId in tagIds.Distinct())
        {
            _db.RestaurantTags.Add(new RestaurantTag { RestaurantID = restaurantId, TagID = tagId });
        }

        await _db.SaveChangesAsync();
    }

    public async Task SetCoverImageAsync(int restaurantId, Image newCoverImage)
    {
        var existingCoverLinks = await _db.RestaurantImages
            .Include(ri => ri.Image)
            .Where(ri => ri.RestaurantID == restaurantId && ri.Image != null && ri.Image.ImageType == "RestaurantCover")
            .ToListAsync();
        _db.RestaurantImages.RemoveRange(existingCoverLinks);

        _db.Images.Add(newCoverImage);
        await _db.SaveChangesAsync();

        _db.RestaurantImages.Add(new RestaurantImage { RestaurantID = restaurantId, ImageID = newCoverImage.ImageID });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveCoverImageAsync(int restaurantId)
    {
        var existingCoverLinks = await _db.RestaurantImages
            .Include(ri => ri.Image)
            .Where(ri => ri.RestaurantID == restaurantId && ri.Image != null && ri.Image.ImageType == "RestaurantCover")
            .ToListAsync();
        _db.RestaurantImages.RemoveRange(existingCoverLinks);
        await _db.SaveChangesAsync();
    }

    public async Task AddEnvironmentImageAsync(int restaurantId, Image image)
    {
        _db.Images.Add(image);
        await _db.SaveChangesAsync();

        _db.RestaurantImages.Add(new RestaurantImage { RestaurantID = restaurantId, ImageID = image.ImageID });
        await _db.SaveChangesAsync();
    }

    public async Task RemoveEnvironmentImageAsync(int restaurantId, int imageId)
    {
        var link = await _db.RestaurantImages
            .FirstOrDefaultAsync(ri => ri.RestaurantID == restaurantId && ri.ImageID == imageId);
        if (link != null)
        {
            _db.RestaurantImages.Remove(link);
            await _db.SaveChangesAsync();
        }
    }

    public async Task SoftDeleteAsync(int id, string reason, int byMemberId)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.RestaurantID == id);
        if (restaurant == null)
        {
            return;
        }

        restaurant.IsDeleted = true;
        var now = _clock.GetNow();
        restaurant.DeletedAt = now;
        restaurant.DeletedBy = byMemberId;
        restaurant.DeleteReason = reason;
        restaurant.UpdatedAt = now;
        await _db.SaveChangesAsync();
    }

    public async Task RestoreAsync(int id)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.RestaurantID == id);
        if (restaurant == null)
        {
            return;
        }

        restaurant.IsDeleted = false;
        restaurant.DeletedAt = null;
        restaurant.DeletedBy = null;
        restaurant.DeleteReason = null;
        restaurant.UpdatedAt = _clock.GetNow();
        await _db.SaveChangesAsync();
    }

    public async Task<RestaurantStats> GetStatsAsync(RestaurantFilterQuery filter)
    {
        // Stats reflect the same Search/City/District/Tag scope as the list below
        // them (not the grand total across every restaurant) — sort/page don't
        // affect scope so they're intentionally left out.
        var active = ApplyCommonFilters(_db.Restaurants.Where(r => !r.IsDeleted), filter.Search, filter.City, filter.District, filter.TagId);
        var disabled = ApplyCommonFilters(_db.Restaurants.Where(r => r.IsDeleted), filter.Search, filter.City, filter.District, filter.TagId);

        var activeRestaurantIds = active.Select(r => r.RestaurantID);
        var reviewAggregate = await _db.Reviews
            .Where(r => activeRestaurantIds.Contains(r.RestaurantID) && !r.IsDeleted && r.Status == "Active")
            .GroupBy(_ => 1)
            .Select(g => new
            {
                ReviewCount = g.Count(),
                AverageRating = g.Average(r => (decimal)r.Rating)
            })
            .FirstOrDefaultAsync();

        return new RestaurantStats
        {
            Total = await active.CountAsync(),
            AvgRating = reviewAggregate == null ? 0m : Math.Round(reviewAggregate.AverageRating, 1),
            ReviewCount = reviewAggregate?.ReviewCount ?? 0,
            DisabledCount = await disabled.CountAsync()
        };
    }

    public async Task<List<string>> GetDistinctCitiesAsync()
    {
        return await _db.Restaurants.Select(r => r.City).Distinct().OrderBy(c => c).ToListAsync();
    }

    public async Task<List<string>> GetDistinctDistrictsAsync()
    {
        return await _db.Restaurants.Select(r => r.District).Distinct().OrderBy(d => d).ToListAsync();
    }

    public async Task<List<string>> GetDistinctDeleteReasonsAsync()
    {
        return await _db.Restaurants
            .Where(r => r.IsDeleted && r.DeleteReason != null)
            .Select(r => r.DeleteReason!)
            .Distinct()
            .OrderBy(reason => reason)
            .ToListAsync();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
