using CourseInventory.Web.Data;
using CourseInventory.Web.Hubs;
using CourseInventory.Web.Models;
using CourseInventory.Web.Services;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Net;
using System.Globalization;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
var runMigrationsOnly = args.Any(arg => string.Equals(arg, "--migrate", StringComparison.OrdinalIgnoreCase));

var connectionString = NormalizePostgresConnectionString(
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"]);

if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Production requires ConnectionStrings__DefaultConnection or DATABASE_URL.");
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    connectionString = "Host=localhost;Port=5432;Database=course_inventory;Username=postgres;Password=postgres";
}

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
    authentication.AddOpenIdConnect("Google", "Google", options =>
    {
        var googleBackchannelHandler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.Authority = "https://accounts.google.com";
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Backchannel = new HttpClient(googleBackchannelHandler)
        {
            Timeout = TimeSpan.FromMinutes(3),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/Account/Login?externalError=google");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"];
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
    authentication.AddFacebook(options =>
    {
        // ASP.NET Core handles /signin-facebook through the remote auth middleware,
        // so the callback path must stay aligned with the Meta Developer redirect URI.
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret;
        options.CallbackPath = "/signin-facebook";
        options.SaveTokens = true;
        options.Scope.Add("email");
        options.Fields.Add("name");
        options.Fields.Add("email");
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/Account/Login?externalError=facebook");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddLocalization();
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddScoped<IAccessService, AccessService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IFieldService, FieldService>();
builder.Services.AddScoped<ICustomIdService, CustomIdService>();
builder.Services.AddScoped<IDiscussionService, DiscussionService>();
builder.Services.AddSingleton<IDiscussionPresenceService, DiscussionPresenceService>();
builder.Services.AddScoped<IMarkdownService, MarkdownService>();
builder.Services.AddScoped<ISearchService, SearchService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddSingleton<IUiTextService, UiTextService>();
builder.Services.AddScoped<IUserActivityService, UserActivityService>();

var app = builder.Build();

if (runMigrationsOnly)
{
    await SeedData.SeedAsync(app.Services);
    return;
}

app.UseForwardedHeaders();

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

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

app.UseResponseCompression();
app.UseRouting();

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = [new CultureInfo("en"), new CultureInfo("fr")],
    SupportedUICultures = [new CultureInfo("en"), new CultureInfo("fr")]
});

app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true &&
        !Path.HasExtension(context.Request.Path.Value) &&
        !context.Request.Path.StartsWithSegments("/hubs"))
    {
        var activity = context.RequestServices.GetRequiredService<IUserActivityService>();
        await activity.TouchAsync(context.User, context.RequestAborted);
    }

    await next();
});
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapHub<InventoryDiscussionHub>("/hubs/inventory-discussion");

try
{
    var shouldMigrateOnStartup = app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("Database:MigrateOnStartup");

    if (!app.Environment.IsEnvironment("Testing") && shouldMigrateOnStartup)
    {
        await SeedData.SeedAsync(app.Services);
    }
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

static string? NormalizePostgresConnectionString(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return null;
    }

    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "postgres" && uri.Scheme != "postgresql"))
    {
        return value;
    }

    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(0) ?? string.Empty);
    var password = Uri.UnescapeDataString(userInfo.ElementAtOrDefault(1) ?? string.Empty);
    var database = uri.AbsolutePath.TrimStart('/');

    var builder = new NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = database,
        Username = username,
        Password = password,
        SslMode = SslMode.Require
    };

    return builder.ConnectionString;
}

public partial class Program;
