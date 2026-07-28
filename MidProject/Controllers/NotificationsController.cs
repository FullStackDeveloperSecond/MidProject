using Microsoft.AspNetCore.Mvc;
using MidProject.Models.ViewModels.Notifications;
using MidProject.Services;
using MidProject.Services.IServices;

namespace MidProject.Controllers;

[Route("Notifications")]
[ServiceFilter(typeof(AdminAuthorizationFilter))]
public sealed class NotificationsController : Controller
{
    private const string ValidationFailedMessage = "資料驗證失敗，請修正標示欄位後再試。";
    private const string NotFoundMessage = "找不到指定的通知。";
    private const string DeletedMessage = "此通知已刪除，無法執行該操作。";
    private const string ConcurrentMessage = "此通知已由其他管理員完成處理，請重新整理後確認最新狀態。";
    private const string FailedMessage = "操作失敗，請稍後再試；若持續發生請聯絡系統管理員。";

    private readonly INotificationService _service;
    private readonly ICurrentAdminAccessor _currentAdmin;

    public NotificationsController(
        INotificationService service,
        ICurrentAdminAccessor currentAdmin)
    {
        _service = service;
        _currentAdmin = currentAdmin;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] NotificationIndexQuery query, CancellationToken cancellationToken)
    {
        var model = await _service.GetIndexAsync(query, cancellationToken);
        if (!ModelState.IsValid)
        {
            model.QueryError = "篩選或排序條件無效，請修正後再試。";
        }
        return View(model);
    }

    [HttpGet("Details/{id:int}")]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _service.GetDetailsAsync(id, cancellationToken);
        return model is null ? MessageView(NotFoundMessage, StatusCodes.Status404NotFound) : View(model);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        return View(await _service.GetCreateFormAsync(cancellationToken: cancellationToken));
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NotificationFormViewModel form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            AddGeneralValidationMessage();
            return View(await _service.GetCreateFormAsync(form, cancellationToken));
        }

        var result = await _service.CreateAsync(form, _currentAdmin.MemberID, HttpContext.TraceIdentifier, cancellationToken);
        if (result.Classification == NotificationCommandClassification.Success)
        {
            TempData["NotificationSuccess"] = "通知建立成功。";
            return RedirectToAction(nameof(Index));
        }

        if (result.Classification == NotificationCommandClassification.ValidationFailed)
        {
            AddErrors(result.ValidationErrors);
            AddGeneralValidationMessage();
            return View(await _service.GetCreateFormAsync(form, cancellationToken));
        }

        ModelState.AddModelError(string.Empty, MessageFor(result.Classification));
        return View(await _service.GetCreateFormAsync(form, cancellationToken));
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var form = await _service.GetEditFormAsync(id, cancellationToken: cancellationToken);
        if (form is not null)
        {
            return View(form);
        }

        var details = await _service.GetDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return MessageView(NotFoundMessage, StatusCodes.Status404NotFound);
        }

        return MessageView(details.IsDeleted ? DeletedMessage : "此通知已發送，無法執行該操作。", StatusCodes.Status409Conflict);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NotificationFormViewModel form, CancellationToken cancellationToken)
    {
        form.NotificationID = id;
        if (!ModelState.IsValid)
        {
            AddGeneralValidationMessage();
            return View(await _service.GetEditFormAsync(id, form, cancellationToken));
        }

        var result = await _service.EditAsync(id, form, _currentAdmin.MemberID, HttpContext.TraceIdentifier, cancellationToken);
        if (result.Classification == NotificationCommandClassification.Success)
        {
            TempData["NotificationSuccess"] = "通知更新成功。";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Classification == NotificationCommandClassification.ValidationFailed)
        {
            AddErrors(result.ValidationErrors);
            AddGeneralValidationMessage();
            return View(await _service.GetEditFormAsync(id, form, cancellationToken));
        }

        ModelState.AddModelError(string.Empty, MessageFor(result.Classification));
        return View(await _service.GetEditFormAsync(id, form, cancellationToken));
    }

    [HttpGet("Delete/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var model = await _service.GetDeleteAsync(id, cancellationToken);
        if (model is not null)
        {
            return View(model);
        }

        var details = await _service.GetDetailsAsync(id, cancellationToken);
        if (details is null)
        {
            return MessageView(NotFoundMessage, StatusCodes.Status404NotFound);
        }

        return MessageView(details.IsDeleted ? DeletedMessage : "此通知已發送，無法執行該操作。", StatusCodes.Status409Conflict);
    }

    [HttpPost("Delete/{id:int}")]
    [ActionName("DeleteConfirmed")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(
        int id,
        [FromForm] string rowVersion,
        CancellationToken cancellationToken)
    {
        var result = await _service.DeleteAsync(id, rowVersion, _currentAdmin.MemberID, HttpContext.TraceIdentifier, cancellationToken);
        if (result.Classification == NotificationCommandClassification.Success)
        {
            TempData["NotificationSuccess"] = "通知刪除成功。";
            return RedirectToAction(nameof(Index));
        }

        var model = await _service.GetDeleteAsync(id, cancellationToken);
        if (model is null)
        {
            return MessageView(MessageFor(result.Classification), StatusCodes.Status409Conflict);
        }

        ModelState.AddModelError(string.Empty, MessageFor(result.Classification));
        return View("Delete", model);
    }

    [HttpPost("Send/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int id, CancellationToken cancellationToken)
    {
        var result = await _service.SendAsync(id, _currentAdmin.MemberID, HttpContext.TraceIdentifier, cancellationToken);
        if (result.Classification == SingleSendClassification.Success)
        {
            TempData["NotificationSuccess"] = "通知發送成功。";
        }
        else
        {
            TempData["NotificationError"] = result.Classification switch
            {
                SingleSendClassification.NotFound => NotFoundMessage,
                SingleSendClassification.Deleted => DeletedMessage,
                SingleSendClassification.AlreadySent => "此通知已發送，無法再次發送。",
                SingleSendClassification.NotDue => "通知尚未到排程時間，無法發送。",
                SingleSendClassification.ConcurrentlyHandled => ConcurrentMessage,
                _ => FailedMessage
            };
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("SendDue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDue(CancellationToken cancellationToken)
    {
        var result = await _service.SendDueAsync(_currentAdmin.MemberID, HttpContext.TraceIdentifier, cancellationToken);
        TempData["NotificationSuccess"] =
            $"批次處理完成：成功 {result.SuccessCount}、已發送 {result.AlreadySentCount}、未到期 {result.NotDueCount}、已取消／刪除 {result.DeletedCount}、失敗 {result.FailedCount}。";
        if (result.FailedCount > 0)
        {
            TempData["NotificationWarning"] = $"處理失敗的通知 ID：{string.Join("、", result.Items.Where(x => x.Classification == BatchItemClassification.Failed).Select(x => x.NotificationID))}";
        }
        return RedirectToAction(nameof(Index));
    }

    private IActionResult MessageView(string message, int statusCode)
    {
        Response.StatusCode = statusCode;
        return View("AccessDenied", new NotificationAccessDeniedViewModel { Message = message });
    }

    private void AddGeneralValidationMessage()
    {
        if (!ModelState.TryGetValue(string.Empty, out var entry) || !entry.Errors.Any(x => x.ErrorMessage == ValidationFailedMessage))
        {
            ModelState.AddModelError(string.Empty, ValidationFailedMessage);
        }
    }

    private void AddErrors(IReadOnlyDictionary<string, string[]>? errors)
    {
        if (errors is null)
        {
            return;
        }

        foreach (var (field, messages) in errors)
        {
            foreach (var message in messages)
            {
                ModelState.AddModelError(field, message);
            }
        }
    }

    private static string MessageFor(NotificationCommandClassification classification) => classification switch
    {
        NotificationCommandClassification.NotFound => NotFoundMessage,
        NotificationCommandClassification.Deleted => DeletedMessage,
        NotificationCommandClassification.ConcurrentlyHandled => ConcurrentMessage,
        NotificationCommandClassification.ReadOnly => "此通知已發送，無法執行該操作。",
        NotificationCommandClassification.ValidationFailed => ValidationFailedMessage,
        _ => FailedMessage
    };
}
