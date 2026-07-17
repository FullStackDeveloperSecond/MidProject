using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MidProject.Models.ViewModels.Notifications;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class NotificationAdminAuthorizationFilter : IAsyncResourceFilter
{
    private readonly ITrustedMemberIdentityAccessor _identityAccessor;
    private readonly INotificationAdminAccessEvaluator _accessEvaluator;
    private readonly ILogger<NotificationAdminAuthorizationFilter> _logger;

    public NotificationAdminAuthorizationFilter(
        ITrustedMemberIdentityAccessor identityAccessor,
        INotificationAdminAccessEvaluator accessEvaluator,
        ILogger<NotificationAdminAuthorizationFilter> logger)
    {
        _identityAccessor = identityAccessor;
        _accessEvaluator = accessEvaluator;
        _logger = logger;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        var identity = await _identityAccessor.GetCurrentAsync(cancellationToken);

        if (!identity.IsAuthenticated)
        {
            var request = context.HttpContext.Request;
            var returnUrl = $"{request.PathBase}{request.Path}{request.QueryString}";
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        var access = await _accessEvaluator.EvaluateAsync(identity.MemberID, cancellationToken);
        if (access.Classification == NotificationAdminAccessClassification.Authorized && access.Context is not null)
        {
            context.HttpContext.Items[NotificationAdminContext.HttpContextItemKey] = access.Context;
            await next();
            return;
        }

        var message = access.Classification switch
        {
            NotificationAdminAccessClassification.IdentityUnmapped => "無法確認登入者身分，請重新登入。",
            NotificationAdminAccessClassification.NotAdministrator => "您沒有權限存取通知管理功能。",
            _ => "此管理員帳號目前不可使用。"
        };

        _logger.LogWarning(
            "Notification authorization rejected at {TaipeiTimestamp}; Result={ResultClassification}; AdminID={AdminID}; CorrelationID={CorrelationID}",
            DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)),
            access.Classification,
            identity.MemberID,
            context.HttpContext.TraceIdentifier);

        context.Result = new ViewResult
        {
            ViewName = "AccessDenied",
            StatusCode = StatusCodes.Status403Forbidden,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<NotificationAccessDeniedViewModel>(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                context.ModelState)
            {
                Model = new NotificationAccessDeniedViewModel { Message = message }
            }
        };
    }
}
