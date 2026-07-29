using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using MidProject.Data;
using MidProject.Repositories;
using MidProject.Repositories.IRepositories;
using MidProject.Services;
using MidProject.Services.IServices;
using System.Threading.RateLimiting;

if (args.Contains(MigrationDriftVerifier.CommandArgument, StringComparer.Ordinal))
{
    Environment.ExitCode = MigrationDriftVerifier.Run();
    return;
}

var seedTestData = args.Contains(
    DevelopmentTestDataSeeder.CommandArgument,
    StringComparer.Ordinal);
if (seedTestData)
{
    args = args
        .Where(argument => !string.Equals(
            argument,
            DevelopmentTestDataSeeder.CommandArgument,
            StringComparison.Ordinal))
        .ToArray();
}

var builder = WebApplication.CreateBuilder(args);
if (seedTestData)
{
    builder.Logging.AddFilter(
        "Microsoft.EntityFrameworkCore.Database.Command",
        LogLevel.Warning);
}

// Local-only connection string / overrides. Matches the `appsettings.*.local.json` pattern
// already reserved in .gitignore. Loaded unconditionally (not gated by ASPNETCORE_ENVIRONMENT)
// so it behaves the same whether launched via `dotnet run` or Visual Studio's debug target.
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>(optional: true);

var notificationLogDirectory = builder.Configuration["NotificationLogging:Directory"];
if (string.IsNullOrWhiteSpace(notificationLogDirectory))
{
    notificationLogDirectory = Path.Combine("App_Data", "logs");
}
if (!Path.IsPathRooted(notificationLogDirectory))
{
    notificationLogDirectory = Path.Combine(
        builder.Environment.ContentRootPath,
        notificationLogDirectory);
}
builder.Logging.AddProvider(new NotificationAuditFileLoggerProvider(
    notificationLogDirectory,
    builder.Configuration.GetValue("NotificationLogging:RetentionDays", 14),
    builder.Configuration.GetValue("NotificationLogging:MaxFileBytes", 20_971_520L),
    TimeProvider.System));

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var notificationIdentityMode = NotificationIdentityModeSelection.Parse(
    Environment.GetEnvironmentVariable("Notifications__IdentityMode"));
builder.Services.AddSingleton(notificationIdentityMode);

// Kefan 餐廳模組（Restaurants/Tags）— Repository + Service 分層
builder.Services.AddScoped<IRestaurantRepository, RestaurantRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<ITagService, TagService>();

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
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.EventsType = typeof(AdminCookieAuthenticationEvents);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// 點數商城（外框商品 + 兌換紀錄）
builder.Services.AddScoped<IAvatarFrameRepository, AvatarFrameRepository>();
builder.Services.AddScoped<IAvatarFrameService, AvatarFrameService>();
builder.Services.AddScoped<IPointsStoreRedemptionService, PointsStoreRedemptionService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IImageLifecycleService, ImageLifecycleService>();
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
    builder.Services.AddScoped<ITrustedMemberIdentityAccessor, AccountLoginTrustedMemberIdentityAccessor>();
}
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITaipeiClock, TaipeiClock>();
builder.Services.AddSingleton<IMemberAudienceCatalog, MemberAudienceCatalog>();
builder.Services.AddScoped<NotificationPresenter>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAdminAccessEvaluator, AdminAccessEvaluator>();
builder.Services.AddScoped<AdminAuthorizationFilter>();
builder.Services.AddScoped<ICurrentAdminAccessor, CurrentAdminAccessor>();
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

if (seedTestData)
{
    if (!app.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "The development test-data seeder can run only in the Development environment.");
    }

    await DevelopmentTestDataSeeder.SeedAsync(app.Services);
    return;
}

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await DevelopmentTestDataSeeder.SeedAsync(app.Services);
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

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        return Task.CompletedTask;
    });
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();

//�ҥ�����
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
