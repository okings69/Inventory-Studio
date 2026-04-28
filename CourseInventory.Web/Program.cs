using CourseInventory.Web.Data;
using CourseInventory.Web.Hubs;
using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=course_inventory;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var authentication = builder.Services.AddAuthentication();

var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
    });
}

var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
    authentication.AddFacebook(options =>
    {
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret;
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddLocalization();
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IFieldService, FieldService>();
builder.Services.AddScoped<ICustomIdService, CustomIdService>();
builder.Services.AddScoped<IDiscussionService, DiscussionService>();
builder.Services.AddScoped<IMarkdownService, MarkdownService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddSingleton<IUiTextService, UiTextService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseRouting();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = [new CultureInfo("en"), new CultureInfo("fr")],
    SupportedUICultures = [new CultureInfo("en"), new CultureInfo("fr")]
});

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapHub<InventoryDiscussionHub>("/hubs/inventory-discussion");

try
{
    await SeedData.SeedAsync(app.Services);
}
catch (PostgresException ex) when (app.Environment.IsDevelopment() && ex.SqlState == PostgresErrorCodes.InvalidPassword)
{
    throw new InvalidOperationException(
        "PostgreSQL rejected the configured credentials. Update CourseInventory.Web/appsettings.Development.json or run database.local-setup.sql in pgAdmin to create the course_inventory user.",
        ex);
}
catch (NpgsqlException ex) when (app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "The application could not connect to PostgreSQL. Check that the PostgreSQL service is running and that CourseInventory.Web/appsettings.Development.json matches your local database.",
        ex);
}

app.Run();
