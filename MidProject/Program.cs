using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Repositories;
using MidProject.Repositories.IRepositories;
using MidProject.Services;
using MidProject.Services.IServices;

var builder = WebApplication.CreateBuilder(args);

// Local-only connection string / overrides. Matches the `appsettings.*.local.json` pattern
// already reserved in .gitignore. Loaded unconditionally (not gated by ASPNETCORE_ENVIRONMENT)
// so it behaves the same whether launched via `dotnet run` or Visual Studio's debug target.
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>(optional: true);

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

var notificationIdentityMode = NotificationIdentityModeSelection.Parse(
    Environment.GetEnvironmentVariable("Notifications__IdentityMode"));
builder.Services.AddSingleton(notificationIdentityMode);

// Kefan 餐廳模組（Restaurants/Tags）— Repository + Service 分層
builder.Services.AddScoped<IRestaurantRepository, RestaurantRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. " +
        "Use .NET User Secrets or the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddScoped<AdminCookieAuthenticationEvents>();

//���U Cookie ���ҪA��
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // �p�G���n�J�ξ��ҥ��ġA�|�۰ʸ���ܦ����|
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Home/Index";
        options.EventsType = typeof(AdminCookieAuthenticationEvents);
    });

builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService>();
if (notificationIdentityMode.Mode == NotificationIdentityMode.DevelopmentTemporary)
{
    builder.Services.AddSingleton<DevelopmentTemporaryTrustedMemberIdentityAccessor>();
    builder.Services.AddSingleton<ITrustedMemberIdentityAccessor>(services =>
        services.GetRequiredService<DevelopmentTemporaryTrustedMemberIdentityAccessor>());
    builder.Services.AddScoped<NotificationIdentityStartupValidator>();
}
else
{
    // AccountLogin：Account/Login 整合正式接上，讀取 AccountController.Login 簽發的 Cookie Claims
    builder.Services.AddScoped<ITrustedMemberIdentityAccessor, CookieClaimsTrustedMemberIdentityAccessor>();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITaipeiClock, TaipeiClock>();
builder.Services.AddSingleton<IMemberAudienceCatalog, MemberAudienceCatalog>();
builder.Services.AddScoped<NotificationPresenter>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotificationAdminAccessEvaluator, NotificationAdminAccessEvaluator>();
builder.Services.AddScoped<NotificationAdminAuthorizationFilter>();
builder.Services.AddScoped<IReportNotificationWindow, ReportNotificationWindow>();
builder.Services.AddScoped<IDashboardNotificationWindow, DashboardNotificationWindow>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// 會員模組（haru）— Repository + Service 分層，比照其他模組：Controller 只碰 IMemberService，不直接用 AppDbContext
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<IMemberService, MemberService>();

// 會員自動懲處：批次邏輯本體是 Scoped 服務，排程背景服務每 30 秒呼叫一次當安全網，
// 管理員也可以在會員列表/編輯頁按「立即重新檢查」手動觸發同一份邏輯（AdminMembersController.RecalculateEscalation）。
// 不再放在 AdminMembersController 的 Index/Edit（GET）裡順便觸發寫入資料庫。
builder.Services.AddScoped<IMemberEscalationService, MemberEscalationService>();
builder.Services.AddHostedService<MemberEscalationBackgroundService>();

var app = builder.Build();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await SeedData.InitializeAsync(app.Services);
	await ReportsTestDataSeeder.SeedAsync(app.Services);
}

await using (var scope = app.Services.CreateAsyncScope())
{
    if (notificationIdentityMode.Mode == NotificationIdentityMode.DevelopmentTemporary)
    {
        await scope.ServiceProvider
            .GetRequiredService<NotificationIdentityStartupValidator>()
            .ValidateAsync();
    }
    else
    {
        _ = scope.ServiceProvider.GetRequiredService<ITrustedMemberIdentityAccessor>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("MidProject.Services.NotificationIdentityStartup");
        logger.LogInformation(
            "Notification identity startup validation; IdentityMode={IdentityMode}; EnvironmentName={EnvironmentName}; ValidationOutcome={ValidationOutcome}; SafeErrorCode={SafeErrorCode}",
            notificationIdentityMode.Mode,
            app.Environment.EnvironmentName,
            "Success",
            null);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

//�ҥ�����
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
