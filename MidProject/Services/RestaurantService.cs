using MidProject.Models;
using MidProject.Models.ViewModels.Restaurants;
using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public class RestaurantService : IRestaurantService
{
    private const int PageSize = 10;

    private readonly IRestaurantRepository _restaurantRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IImageUploadService _imageUploadService;

    public RestaurantService(IRestaurantRepository restaurantRepository, ITagRepository tagRepository, IImageUploadService imageUploadService)
    {
        _restaurantRepository = restaurantRepository;
        _tagRepository = tagRepository;
        _imageUploadService = imageUploadService;
    }

    public async Task<RestaurantIndexViewModel> GetIndexAsync(RestaurantFilterQuery filter)
    {
        var stats = await _restaurantRepository.GetStatsAsync(filter);
        var (items, total) = await _restaurantRepository.GetActivePagedAsync(filter, PageSize);
        var activeTags = (await _tagRepository.GetAllAsync()).Where(t => !t.IsDeleted).ToList();

        return new RestaurantIndexViewModel
        {
            StatsTotal = stats.Total,
            StatsAvgRating = stats.AvgRating,
            StatsReviewCount = stats.ReviewCount,
            StatsDisabledCount = stats.DisabledCount,
            Items = items.Select(MapRow).ToList(),
            Filter = filter,
            AvailableCities = RestaurantOptions.Cities,
            AvailableDistricts = RestaurantOptions.Districts,
            AvailableTags = activeTags.Select(t => (t.TagID, t.TagName)).ToList(),
            TotalItems = total,
            PageSize = PageSize,
            CurrentPage = Math.Max(1, filter.Page)
        };
    }

    public async Task<RestaurantDeletedIndexViewModel> GetDeletedIndexAsync(RestaurantDeletedFilterQuery filter)
    {
        var items = await _restaurantRepository.GetDeletedAsync(filter);
        var rows = items.Select(MapDeletedRow).ToList();
        var reasons = await _restaurantRepository.GetDistinctDeleteReasonsAsync();

        return new RestaurantDeletedIndexViewModel
        {
            StatsDisabledTotal = rows.Count,
            StatsMostRecentName = rows.FirstOrDefault()?.Name,
            StatsMostRecentAt = rows.FirstOrDefault()?.DeletedAt,
            StatsLastReason = rows.FirstOrDefault()?.DeleteReason,
            Items = rows,
            Filter = filter,
            AvailableCities = RestaurantOptions.Cities,
            AvailableReasons = reasons
        };
    }

    public async Task<RestaurantDetailViewModel?> GetDetailAsync(int id)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(id);
        return restaurant == null ? null : MapDetail(restaurant);
    }

    public async Task<RestaurantFormViewModel> GetCreateFormAsync()
    {
        var activeTags = (await _tagRepository.GetAllAsync()).Where(t => !t.IsDeleted).ToList();

        return new RestaurantFormViewModel
        {
            AvailableTags = activeTags.Select(t => new TagOptionViewModel { Id = t.TagID, Name = t.TagName, Selected = false }).ToList(),
            Hours = BuildDefaultHourRows()
        };
    }

    public async Task<RestaurantFormViewModel?> GetEditFormAsync(int id)
    {
        var restaurant = await _restaurantRepository.GetByIdAsync(id);
        if (restaurant == null)
        {
            return null;
        }

        var selectedTagIds = restaurant.RestaurantTags.Select(rt => rt.TagID).ToHashSet();
        var allTags = await _tagRepository.GetAllAsync();
        var availableTags = allTags
            .Where(t => !t.IsDeleted || selectedTagIds.Contains(t.TagID))
            .Select(t => new TagOptionViewModel { Id = t.TagID, Name = t.TagName, Selected = selectedTagIds.Contains(t.TagID) })
            .ToList();

        var cover = restaurant.RestaurantImages.FirstOrDefault(ri => ri.Image != null && ri.Image.ImageType == "RestaurantCover")?.Image;
        var environments = restaurant.RestaurantImages
            .Where(ri => ri.Image != null && ri.Image.ImageType == "RestaurantEnvironment")
            .Select(ri => new ExistingImageViewModel { ImageId = ri.Image!.ImageID, Url = ri.Image.ImageURL })
            .ToList();

        return new RestaurantFormViewModel
        {
            Id = restaurant.RestaurantID,
            Name = restaurant.Name,
            Phone = restaurant.Phone,
            City = restaurant.City,
            District = restaurant.District,
            DetailedAddress = restaurant.DetailedAddress,
            Note = restaurant.Note,
            Latitude = restaurant.Latitude,
            Longitude = restaurant.Longitude,
            SelectedTagIds = selectedTagIds.ToList(),
            AvailableTags = availableTags,
            Hours = BuildHourRowsFromEntities(restaurant.BusinessHours.ToList()),
            ExistingCoverImageId = cover?.ImageID,
            ExistingCoverImageUrl = cover?.ImageURL,
            ExistingEnvironmentImages = environments
        };
    }

    public async Task<RestaurantFormViewModel> RehydrateFormAsync(RestaurantFormViewModel form)
    {
        var selectedTagIds = form.SelectedTagIds.ToHashSet();
        var allTags = await _tagRepository.GetAllAsync();
        form.AvailableTags = allTags
            .Where(t => !t.IsDeleted || selectedTagIds.Contains(t.TagID))
            .Select(t => new TagOptionViewModel { Id = t.TagID, Name = t.TagName, Selected = selectedTagIds.Contains(t.TagID) })
            .ToList();

        if (form.Hours.Count == 0)
        {
            form.Hours = BuildDefaultHourRows();
        }

        if (form.Id.HasValue)
        {
            var restaurant = await _restaurantRepository.GetByIdAsync(form.Id.Value);
            if (restaurant != null)
            {
                var cover = restaurant.RestaurantImages.FirstOrDefault(ri => ri.Image != null && ri.Image.ImageType == "RestaurantCover")?.Image;
                form.ExistingCoverImageId ??= cover?.ImageID;
                form.ExistingCoverImageUrl ??= cover?.ImageURL;
                if (form.ExistingEnvironmentImages.Count == 0)
                {
                    form.ExistingEnvironmentImages = restaurant.RestaurantImages
                        .Where(ri => ri.Image != null && ri.Image.ImageType == "RestaurantEnvironment")
                        .Select(ri => new ExistingImageViewModel { ImageId = ri.Image!.ImageID, Url = ri.Image.ImageURL })
                        .ToList();
                }
            }
        }

        return form;
    }

    public async Task<(bool Success, int? NewId)> CreateAsync(RestaurantFormViewModel form)
    {
        if (form.SelectedTagIds.Count == 0)
        {
            return (false, null);
        }

        var adminId = await _restaurantRepository.GetDefaultAdminMemberIdAsync();

        var restaurant = new Restaurant
        {
            Name = form.Name.Trim(),
            City = form.City,
            District = form.District,
            DetailedAddress = form.DetailedAddress.Trim(),
            Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim(),
            Note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim(),
            Latitude = form.Latitude,
            Longitude = form.Longitude,
            MemberID = adminId,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        await _restaurantRepository.AddAsync(restaurant);
        await _restaurantRepository.ReplaceTagsAsync(restaurant.RestaurantID, form.SelectedTagIds);
        await _restaurantRepository.ReplaceBusinessHoursAsync(restaurant.RestaurantID, BuildHourEntitiesFromRows(form.Hours));

        await ApplyImageChangesAsync(restaurant.RestaurantID, form, adminId);

        return (true, restaurant.RestaurantID);
    }

    public async Task<bool> EditAsync(int id, RestaurantFormViewModel form)
    {
        if (form.SelectedTagIds.Count == 0)
        {
            return false;
        }

        var restaurant = await _restaurantRepository.GetByIdAsync(id);
        if (restaurant == null)
        {
            return false;
        }

        var adminId = await _restaurantRepository.GetDefaultAdminMemberIdAsync();

        restaurant.Name = form.Name.Trim();
        restaurant.City = form.City;
        restaurant.District = form.District;
        restaurant.DetailedAddress = form.DetailedAddress.Trim();
        restaurant.Phone = string.IsNullOrWhiteSpace(form.Phone) ? null : form.Phone.Trim();
        restaurant.Note = string.IsNullOrWhiteSpace(form.Note) ? null : form.Note.Trim();
        restaurant.Latitude = form.Latitude;
        restaurant.Longitude = form.Longitude;
        restaurant.UpdatedAt = DateTime.Now;
        await _restaurantRepository.SaveChangesAsync();

        await _restaurantRepository.ReplaceTagsAsync(id, form.SelectedTagIds);
        await _restaurantRepository.ReplaceBusinessHoursAsync(id, BuildHourEntitiesFromRows(form.Hours));

        await ApplyImageChangesAsync(id, form, adminId);

        return true;
    }

    public Task DisableAsync(int id, string reason, int? byMemberId = null)
    {
        return DisableInternalAsync(id, reason, byMemberId);
    }

    private async Task DisableInternalAsync(int id, string reason, int? byMemberId)
    {
        var memberId = byMemberId ?? await _restaurantRepository.GetDefaultAdminMemberIdAsync();
        var trimmedReason = string.IsNullOrWhiteSpace(reason) ? "違規內容" : reason.Trim();
        await _restaurantRepository.SoftDeleteAsync(id, trimmedReason, memberId);
    }

    public Task RestoreAsync(int id)
    {
        return _restaurantRepository.RestoreAsync(id);
    }

    private async Task ApplyImageChangesAsync(int restaurantId, RestaurantFormViewModel form, int adminId)
    {
        if (form.RemoveCoverImage && form.CoverImageFile == null)
        {
            await _restaurantRepository.RemoveCoverImageAsync(restaurantId);
        }

        if (form.CoverImageFile != null)
        {
            var coverImage = await _imageUploadService.SaveAsync(form.CoverImageFile, "RestaurantCover", adminId);
            await _restaurantRepository.SetCoverImageAsync(restaurantId, coverImage);
        }

        foreach (var imageId in form.RemoveEnvironmentImageIds)
        {
            await _restaurantRepository.RemoveEnvironmentImageAsync(restaurantId, imageId);
        }

        foreach (var file in form.EnvironmentImageFiles)
        {
            var envImage = await _imageUploadService.SaveAsync(file, "RestaurantEnvironment", adminId);
            await _restaurantRepository.AddEnvironmentImageAsync(restaurantId, envImage);
        }
    }

    private static RestaurantRowViewModel MapRow(Restaurant r)
    {
        return new RestaurantRowViewModel
        {
            Id = r.RestaurantID,
            Name = r.Name,
            City = r.City,
            District = r.District,
            Phone = r.Phone,
            Tags = r.RestaurantTags.Where(rt => rt.Tag != null && !rt.Tag.IsDeleted).Select(rt => rt.Tag!.TagName).ToList(),
            Rating = r.AverageRating,
            ReviewCount = r.ReviewCount
        };
    }

    private static RestaurantDeletedRowViewModel MapDeletedRow(Restaurant r)
    {
        return new RestaurantDeletedRowViewModel
        {
            Id = r.RestaurantID,
            Name = r.Name,
            City = r.City,
            District = r.District,
            Tags = r.RestaurantTags.Where(rt => rt.Tag != null && !rt.Tag.IsDeleted).Select(rt => rt.Tag!.TagName).ToList(),
            DeletedAt = r.DeletedAt,
            DeleteReason = r.DeleteReason,
            DeletedByName = r.DeletedByMember?.NickName ?? r.DeletedByMember?.UserName
        };
    }

    private static RestaurantDetailViewModel MapDetail(Restaurant r)
    {
        var cover = r.RestaurantImages.FirstOrDefault(ri => ri.Image != null && ri.Image.ImageType == "RestaurantCover")?.Image;
        var environments = r.RestaurantImages
            .Where(ri => ri.Image != null && ri.Image.ImageType == "RestaurantEnvironment")
            .Select(ri => ri.Image!.ImageURL)
            .ToList();

        return new RestaurantDetailViewModel
        {
            Id = r.RestaurantID,
            Name = r.Name,
            City = r.City,
            District = r.District,
            DetailedAddress = r.DetailedAddress,
            Phone = r.Phone,
            Note = r.Note,
            Tags = r.RestaurantTags.Where(rt => rt.Tag != null && !rt.Tag.IsDeleted)
                .Select(rt => new TagOptionViewModel { Id = rt.Tag!.TagID, Name = rt.Tag.TagName })
                .ToList(),
            OwnerName = r.Member?.NickName ?? r.Member?.UserName ?? "-",
            OwnerMemberId = r.MemberID,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            AverageRating = r.AverageRating,
            ReviewCount = r.ReviewCount,
            HoursByDay = BuildHourGroupsFromEntities(r.BusinessHours.ToList()),
            CoverImageUrl = cover?.ImageURL,
            EnvironmentImageUrls = environments,
            IsDeleted = r.IsDeleted,
            DeletedAt = r.DeletedAt,
            DeletedByName = r.DeletedByMember?.NickName ?? r.DeletedByMember?.UserName,
            DeleteReason = r.DeleteReason
        };
    }

    private static List<BusinessHourFormRow> BuildDefaultHourRows()
    {
        var rows = new List<BusinessHourFormRow>();
        for (var day = 1; day <= 7; day++)
        {
            rows.Add(new BusinessHourFormRow
            {
                DayOfWeek = day,
                DayLabel = RestaurantOptions.LabelForDay(day),
                IsClosed = false,
                Slots = new List<TimeSlotRow> { new() { Open = new TimeOnly(11, 0), Close = new TimeOnly(21, 0) } }
            });
        }

        return rows;
    }

    private static List<BusinessHourFormRow> BuildHourRowsFromEntities(List<BusinessHour> hours)
    {
        var rows = new List<BusinessHourFormRow>();
        for (var day = 1; day <= 7; day++)
        {
            var dayHours = hours.Where(h => h.DayOfWeek == day).ToList();
            var isClosed = dayHours.Count == 0 || dayHours.All(h => h.IsClosed);
            var slots = dayHours.Where(h => !h.IsClosed).OrderBy(h => h.OpenTime)
                .Select(h => new TimeSlotRow { Open = h.OpenTime, Close = h.CloseTime }).ToList();

            if (!isClosed && slots.Count == 0)
            {
                slots.Add(new TimeSlotRow { Open = new TimeOnly(11, 0), Close = new TimeOnly(21, 0) });
            }

            rows.Add(new BusinessHourFormRow
            {
                DayOfWeek = day,
                DayLabel = RestaurantOptions.LabelForDay(day),
                IsClosed = isClosed,
                Slots = slots
            });
        }

        return rows;
    }

    private static List<BusinessHourGroupViewModel> BuildHourGroupsFromEntities(List<BusinessHour> hours)
    {
        var groups = new List<BusinessHourGroupViewModel>();
        for (var day = 1; day <= 7; day++)
        {
            var dayHours = hours.Where(h => h.DayOfWeek == day).ToList();
            var isClosed = dayHours.Count == 0 || dayHours.All(h => h.IsClosed);
            var slots = dayHours.Where(h => !h.IsClosed).OrderBy(h => h.OpenTime)
                .Select(h => new TimeSlotRow { Open = h.OpenTime, Close = h.CloseTime }).ToList();

            groups.Add(new BusinessHourGroupViewModel
            {
                DayOfWeek = day,
                DayLabel = RestaurantOptions.LabelForDay(day),
                IsClosed = isClosed,
                Slots = slots
            });
        }

        return groups;
    }

    private static List<BusinessHour> BuildHourEntitiesFromRows(List<BusinessHourFormRow> rows)
    {
        var result = new List<BusinessHour>();
        foreach (var row in rows)
        {
            if (row.IsClosed || row.Slots.Count == 0)
            {
                result.Add(new BusinessHour
                {
                    DayOfWeek = row.DayOfWeek,
                    OpenTime = new TimeOnly(0, 0),
                    CloseTime = new TimeOnly(0, 0),
                    IsClosed = true
                });
                continue;
            }

            foreach (var slot in row.Slots)
            {
                result.Add(new BusinessHour
                {
                    DayOfWeek = row.DayOfWeek,
                    OpenTime = slot.Open,
                    CloseTime = slot.Close,
                    IsClosed = false
                });
            }
        }

        return result;
    }
}
