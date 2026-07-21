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
// AccountLogin deliberately does not register an implementation here. The externally
// owned Account/Login integration must supply ITrustedMemberIdentityAccessor.
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
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
