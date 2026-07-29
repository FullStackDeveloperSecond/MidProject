namespace MidProject.Services;

public sealed record AdminContext(int MemberID)
{
    internal const string HttpContextItemKey = "MidProject.ValidatedAdmin";

    public static bool IsValidated(HttpContext httpContext) =>
        httpContext.Items[HttpContextItemKey] is AdminContext;

    public static AdminContext FromHttpContext(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(HttpContextItemKey, out var value) && value is AdminContext context)
        {
            return context;
        }

        throw new InvalidOperationException("A validated administrator context is required.");
    }
}
