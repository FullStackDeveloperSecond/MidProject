using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MidProject.Models.ViewModels;
using MidProject.Services.IServices;

namespace MidProject.Services;

public sealed class AdminAuthorizationFilter : IAsyncResourceFilter
{
    private readonly ITrustedMemberIdentityAccessor _identityAccessor;
    private readonly IAdminAccessEvaluator _accessEvaluator;
    private readonly ITaipeiClock _clock;
    private readonly ILogger<AdminAuthorizationFilter> _logger;

    public AdminAuthorizationFilter(
        ITrustedMemberIdentityAccessor identityAccessor,
        IAdminAccessEvaluator accessEvaluator,
        ITaipeiClock clock,
        ILogger<AdminAuthorizationFilter> logger)
    {
        _identityAccessor = identityAccessor;
        _accessEvaluator = accessEvaluator;
        _clock = clock;
        _logger = logger;
    }

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var cancellationToken = context.HttpContext.RequestAborted;
        var identity = await _identityAccessor.GetCurrentAsync(cancellationToken);

        if (!identity.IsAuthenticated)
        {
            var request = context.HttpContext.Request;
            LogRejected("Unauthenticated", null, request.Path, context.HttpContext.TraceIdentifier);
            var returnUrl = $"{request.PathBase}{request.Path}{request.QueryString}";
            context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
            return;
        }

        var access = await _accessEvaluator.EvaluateAsync(identity.MemberID, cancellationToken);
        if (access.Classification == AdminAccessClassification.Authorized && access.Context is not null)
        {
            context.HttpContext.Items[AdminContext.HttpContextItemKey] = access.Context;
            await next();
            return;
        }

        var message = access.Classification switch
        {
            AdminAccessClassification.IdentityUnmapped => "無法確認登入者身分，請重新登入。",
            AdminAccessClassification.NotAdministrator => "您沒有權限存取後台管理功能。",
            _ => "此管理員帳號目前不可使用。"
        };

        LogRejected(
            access.Classification.ToString(),
            access.Context?.MemberID,
            context.HttpContext.Request.Path,
            context.HttpContext.TraceIdentifier);

        context.Result = new ViewResult
        {
            ViewName = "AdminAccessDenied",
            StatusCode = StatusCodes.Status403Forbidden,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary<AdminAccessDeniedViewModel>(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                context.ModelState)
            {
                Model = new AdminAccessDeniedViewModel { Message = message }
            }
        };
    }

    private void LogRejected(
        string classification,
        int? validatedAdminId,
        PathString path,
        string correlationId)
    {
        _logger.LogWarning(
            "Admin authorization rejected at {TaipeiTimestamp}; Operation=AuthorizeAdminArea; Path={Path}; Result={ResultClassification}; AdminID={AdminID}; CorrelationID={CorrelationID}",
            _clock.GetNow(),
            path,
            classification,
            validatedAdminId,
            correlationId);
    }
}
