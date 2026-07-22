namespace MidProject.Services.IServices;

public interface IMemberAudienceCatalog
{
    IReadOnlyDictionary<string, string> Roles { get; }
    IReadOnlyDictionary<string, string> Statuses { get; }
    bool IsValidRole(string? value);
    bool IsValidStatus(string? value);
    string GetRoleLabel(string value);
    string GetStatusLabel(string value);
}
