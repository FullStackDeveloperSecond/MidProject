using Microsoft.EntityFrameworkCore;
using MidProject.Data;

namespace MidProject.Services;

public sealed class NotificationIdentityStartupValidator
{
    private const string DemoAdminEmail = "admin@example.com";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly DevelopmentTemporaryTrustedMemberIdentityAccessor _identityAccessor;
    private readonly ILogger<NotificationIdentityStartupValidator> _logger;

    public NotificationIdentityStartupValidator(
        AppDbContext dbContext,
        IWebHostEnvironment environment,
        DevelopmentTemporaryTrustedMemberIdentityAccessor identityAccessor,
        ILogger<NotificationIdentityStartupValidator> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _identityAccessor = identityAccessor;
        _logger = logger;
    }

    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.Equals(_environment.EnvironmentName, "Development", StringComparison.Ordinal))
            {
                throw StartupFailure(
                    "NID-TEMP-NONDEVELOPMENT",
                    "DevelopmentTemporary notification identity requires the Development environment.");
            }

            var members = await _dbContext.Members
                .AsNoTracking()
                .Where(member => member.Email == DemoAdminEmail)
                .Select(member => new
                {
                    member.MemberID,
                    member.Role,
                    member.Status,
                    member.IsActive,
                    member.IsLocked,
                    member.IsDeleted
                })
                .Take(2)
                .ToListAsync(cancellationToken);

            if (members.Count == 0)
            {
                throw StartupFailure(
                    "NID-TEMP-ADMIN-NOT-FOUND",
                    "The fixed notification Demo Admin was not found.");
            }

            if (members.Count > 1)
            {
                throw StartupFailure(
                    "NID-TEMP-ADMIN-NOT-UNIQUE",
                    "The fixed notification Demo Admin is not unique.");
            }

            var member = members[0];
            if (member.Role != "Admin" ||
                member.Status != "Normal" ||
                !member.IsActive ||
                member.IsLocked ||
                member.IsDeleted)
            {
                throw StartupFailure(
                    "NID-TEMP-ADMIN-INELIGIBLE",
                    "The fixed notification Demo Admin is not eligible.");
            }

            _identityAccessor.Seal(member.MemberID);
            Log("Success", null);
        }
        catch (NotificationIdentityStartupException exception)
        {
            Log("Failed", exception.SafeCode);
            throw;
        }
    }

    private static NotificationIdentityStartupException StartupFailure(string safeCode, string message) =>
        new(safeCode, message);

    private void Log(string outcome, string? safeCode)
    {
        _logger.LogInformation(
            "Notification identity startup validation; IdentityMode={IdentityMode}; EnvironmentName={EnvironmentName}; ValidationOutcome={ValidationOutcome}; SafeErrorCode={SafeErrorCode}",
            NotificationIdentityMode.DevelopmentTemporary,
            _environment.EnvironmentName,
            outcome,
            safeCode);
    }
}
