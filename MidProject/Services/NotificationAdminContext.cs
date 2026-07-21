namespace MidProject.Services;

public sealed record NotificationAdminContext(int MemberID)
{
    internal const string HttpContextItemKey = "MidProject.Notifications.ValidatedAdmin";

    public static NotificationAdminContext FromHttpContext(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(HttpContextItemKey, out var value) && value is NotificationAdminContext context)
        {
            return context;
        }

        throw new InvalidOperationException("A validated notification administrator context is required.");
    }
}
