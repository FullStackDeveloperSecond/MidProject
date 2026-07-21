namespace MidProject.Services;

public sealed class NotificationIdentityStartupException : Exception
{
    public NotificationIdentityStartupException(string safeCode, string message)
        : base($"{safeCode}: {message}")
    {
        SafeCode = safeCode;
    }

    public string SafeCode { get; }
}
