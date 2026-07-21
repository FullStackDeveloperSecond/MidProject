using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Models;
using MidProject.Models.ViewModels.Restaurants;
using MidProject.Repositories.IRepositories;

namespace MidProject.Repositories;

public class RestaurantRepository : IRestaurantRepository
{
    private readonly AppDbContext _db;

    public RestaurantRepository(AppDbContext db)
    {
        _db = db;
    }

    private IQueryable<Restaurant> BaseQuery()
    {
        return _db.Restaurants
            .Include(r => r.Member)
            .Include(r => r.DeletedByMember)
            .Include(r => r.BusinessHours)
            .Include(r => r.RestaurantTags).ThenInclude(rt => rt.Tag)
            .Include(r => r.RestaurantImages).ThenInclude(ri => ri.Image);
    }

    public async Task<(List<Restaurant> Items, int TotalCount)> GetActivePagedAsync(RestaurantFilterQuery filter, int pageSize)
    {
        var query = BaseQuery().Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var keyword = filter.Search.Trim();
            query = query.Where(r =>
                r.Name.Contains(keyword) ||
                r.DetailedAddress.Contains(keyword) ||
                (r.Note != null && r.Note.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            query = query.Where(r => r.City == filter.City);
        }

        if (!string.IsNullOrWhiteSpace(filter.District))
        {
            query = query.Where(r => r.District == filter.District);
        }

        if (filter.TagId.HasValue)
        {
            query = query.Where(r => r.RestaurantTags.Any(rt => rt.TagID == filter.TagId.Value));
        }

        query = filter.Sort switch
        {
            "rating" => query.OrderByDescending(r => r.AverageRating),
            "review" => query.OrderByDescending(r => r.ReviewCount),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var page = Math.Max(1, filter.Page);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Restaurant>> GetDeletedAsync(RestaurantDeletedFilterQuery filter)
    {
        var query = BaseQuery().Where(r => r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var keyword = filter.Search.Trim();
            query = query.Where(r =>
                r.Name.Contains(keyword) ||
                r.DetailedAddress.Contains(keyword) ||
                (r.Note != null && r.Note.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            query = query.Where(r => r.City == filter.City);
        }

        if (!string.IsNullOrWhiteSpace(filter.Reason))
        {
            query = query.Where(r => r.DeleteReason == filter.Reason);
        }

        return await query.OrderByDescending(r => r.DeletedAt ?? r.CreatedAt).ToListAsync();
    }

    public async Task<Restaurant?> GetByIdAsync(int id)
    {
        return await BaseQuery().FirstOrDefaultAsync(r => r.RestaurantID == id);
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
        restaurant.DeletedAt = DateTime.Now;
        restaurant.DeletedBy = byMemberId;
        restaurant.DeleteReason = reason;
        restaurant.UpdatedAt = DateTime.Now;
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
        restaurant.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
    }

    public async Task<int> GetDefaultAdminMemberIdAsync()
    {
        var adminId = await _db.Members.Where(m => m.UserName == "admin").Select(m => m.MemberID).FirstOrDefaultAsync();
        if (adminId != 0)
        {
            return adminId;
        }

        return await _db.Members.OrderBy(m => m.MemberID).Select(m => m.MemberID).FirstOrDefaultAsync();
    }

    public async Task<RestaurantStats> GetStatsAsync()
    {
        var active = _db.Restaurants.Where(r => !r.IsDeleted);

        return new RestaurantStats
        {
            Total = await active.CountAsync(),
            AvgRating = await active.AnyAsync() ? Math.Round(await active.AverageAsync(r => r.AverageRating), 1) : 0m,
            ReviewCount = await active.SumAsync(r => r.ReviewCount),
            DisabledCount = await _db.Restaurants.CountAsync(r => r.IsDeleted)
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
