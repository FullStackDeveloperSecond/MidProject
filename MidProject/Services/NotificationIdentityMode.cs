namespace MidProject.Services;

public enum NotificationIdentityMode
{
    DevelopmentTemporary,
    AccountLogin
}

public sealed record NotificationIdentityModeSelection(NotificationIdentityMode Mode)
{
    public static NotificationIdentityModeSelection Parse(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            throw new NotificationIdentityStartupException(
                "NID-MODE-MISSING",
                "Notifications identity mode is required.");
        }

        var mode = configuredValue switch
        {
            nameof(NotificationIdentityMode.DevelopmentTemporary) => NotificationIdentityMode.DevelopmentTemporary,
            nameof(NotificationIdentityMode.AccountLogin) => NotificationIdentityMode.AccountLogin,
            _ => throw new NotificationIdentityStartupException(
                "NID-MODE-INVALID",
                "Notifications identity mode is invalid.")
        };

        return new NotificationIdentityModeSelection(mode);
    }
}
