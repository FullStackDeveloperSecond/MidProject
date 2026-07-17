using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class MemberAudienceCatalog : IMemberAudienceCatalog
{
    public IReadOnlyDictionary<string, string> Roles { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["User"] = "一般會員",
        ["Admin"] = "管理員"
    };

    public IReadOnlyDictionary<string, string> Statuses { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Normal"] = "正常",
        ["Warning"] = "警告",
        ["Muted"] = "禁言",
        ["Suspended"] = "停權",
        ["Deleted"] = "已刪除"
    };

    public bool IsValidRole(string? value) => value is not null && Roles.ContainsKey(value);
    public bool IsValidStatus(string? value) => value is not null && Statuses.ContainsKey(value);
    public string GetRoleLabel(string value) => Roles.TryGetValue(value, out var label) ? label : value;
    public string GetStatusLabel(string value) => Statuses.TryGetValue(value, out var label) ? label : value;
}
