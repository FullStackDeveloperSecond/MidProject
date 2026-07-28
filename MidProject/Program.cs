using Microsoft.EntityFrameworkCore;
using MidProject.Data;
using MidProject.Repositories;
using MidProject.Repositories.IRepositories;
using MidProject.Services;
using MidProject.Services.IServices;

if (args.Contains(MigrationDriftVerifier.CommandArgument, StringComparer.Ordinal))
{
    Environment.ExitCode = MigrationDriftVerifier.Run();
    return;
}

var builder = WebApplication.CreateBuilder(args);

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

var notificationIdentityMode = NotificationIdentityModeSelection.Parse(
    Environment.GetEnvironmentVariable("Notifications__IdentityMode"));
builder.Services.AddSingleton(notificationIdentityMode);

// Kefan 餐廳模組（Restaurants/Tags）— Repository + Service 分層
builder.Services.AddScoped<IRestaurantRepository, RestaurantRepository>();
builder.Services.AddScoped<ITagRepository, TagRepository>();
builder.Services.AddScoped<IRestaurantService, RestaurantService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();
builder.Services.AddScoped<IImageLifecycleService, ImageLifecycleService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'DefaultConnection' is not configured. " +
        "Use .NET User Secrets or the ConnectionStrings__DefaultConnection environment variable.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

//���U Cookie ���ҪA��
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        // �p�G���n�J�ξ��ҥ��ġA�|�۰ʸ���ܦ����|
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
    });

builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService>();

// 點數商城（外框商品 + 兌換紀錄）
builder.Services.AddScoped<IAvatarFrameRepository, AvatarFrameRepository>();
builder.Services.AddScoped<IAvatarFrameService, AvatarFrameService>();
builder.Services.AddScoped<IPointsStoreRedemptionService, PointsStoreRedemptionService>();
builder.Services.AddHttpContextAccessor();
if (notificationIdentityMode.Mode == NotificationIdentityMode.DevelopmentTemporary)
{
    builder.Services.AddSingleton<DevelopmentTemporaryTrustedMemberIdentityAccessor>();
    builder.Services.AddSingleton<ITrustedMemberIdentityAccessor>(services =>
        services.GetRequiredService<DevelopmentTemporaryTrustedMemberIdentityAccessor>());
    builder.Services.AddScoped<NotificationIdentityStartupValidator>();
}
else
{
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
var app = builder.Build();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await SeedData.InitializeAsync(app.Services);
    await PointsStoreDemoSeeder.InitializeAsync(app.Services);
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
