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
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IReviewService, ReviewService>();
var app = builder.Build();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedData:Enabled"))
{
    await SeedData.InitializeAsync(app.Services);
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
