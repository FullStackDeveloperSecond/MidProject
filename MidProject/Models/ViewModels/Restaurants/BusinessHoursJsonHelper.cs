using System.Text.Json;

namespace MidProject.Models.ViewModels.Restaurants;

public static class BusinessHoursJsonHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private class SlotDto
    {
        public string Open { get; set; } = "11:00";
        public string Close { get; set; } = "21:00";
    }

    private class DayDto
    {
        public int Day { get; set; }
        public bool Closed { get; set; }
        public List<SlotDto> Slots { get; set; } = new();
    }

    public static string ToJson(List<BusinessHourFormRow> rows)
    {
        var dto = rows.Select(r => new DayDto
        {
            Day = r.DayOfWeek,
            Closed = r.IsClosed,
            Slots = r.Slots.Select(s => new SlotDto { Open = s.Open.ToString("HH:mm"), Close = s.Close.ToString("HH:mm") }).ToList()
        }).ToList();

        return JsonSerializer.Serialize(dto, JsonOptions);
    }

    public static List<BusinessHourFormRow> FromJson(string? json)
    {
        var rows = new List<BusinessHourFormRow>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return rows;
        }

        List<DayDto>? dto;
        try
        {
            dto = JsonSerializer.Deserialize<List<DayDto>>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return rows;
        }

        if (dto == null)
        {
            return rows;
        }

        foreach (var d in dto)
        {
            var slots = d.Slots
                .Where(s => TimeOnly.TryParse(s.Open, out _) && TimeOnly.TryParse(s.Close, out _))
                .Select(s => new TimeSlotRow { Open = TimeOnly.Parse(s.Open), Close = TimeOnly.Parse(s.Close) })
                .ToList();

            rows.Add(new BusinessHourFormRow
            {
                DayOfWeek = d.Day,
                DayLabel = RestaurantOptions.LabelForDay(d.Day),
                IsClosed = d.Closed,
                Slots = slots
            });
        }

        return rows;
    }
}
