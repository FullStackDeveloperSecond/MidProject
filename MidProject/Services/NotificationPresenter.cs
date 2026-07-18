using MidProject.Repositories.IRepositories;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class NotificationPresenter
{
    private readonly IMemberAudienceCatalog _catalog;

    public NotificationPresenter(IMemberAudienceCatalog catalog)
    {
        _catalog = catalog;
    }

    public string GetAudienceSummary(NotificationReadRecord source)
    {
        if (source.NotificationType == "Personal")
        {
            var id = source.MemberID.GetValueOrDefault();
            if (!source.MemberExists)
            {
                return $"會員 ID {id}（查無資料）";
            }

            if (source.MemberIsDeleted)
            {
                return $"會員 ID {id}（已刪除）";
            }

            return $"會員 ID {id}｜{source.MemberName}";
        }

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(source.TargetRole))
        {
            parts.Add($"角色：{_catalog.GetRoleLabel(source.TargetRole)}");
        }

        if (!string.IsNullOrEmpty(source.TargetStatus))
        {
            parts.Add($"狀態：{_catalog.GetStatusLabel(source.TargetStatus)}");
        }

        if (source.TargetLevelID.HasValue)
        {
            var id = source.TargetLevelID.Value;
            var level = !source.LevelExists
                ? $"等級 ID {id}（查無資料）"
                : source.LevelIsDeleted
                    ? $"等級 ID {id}（已刪除）"
                    : source.LevelName!;
            parts.Add($"等級：{level}");
        }

        return parts.Count == 0 ? "全部會員" : string.Join("｜", parts);
    }
}
