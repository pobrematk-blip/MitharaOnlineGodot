using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using Mithara.Web.Data;
using Mithara.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var gameConnStr = Environment.GetEnvironmentVariable("MITHARA_WEB_DB")
    ?? builder.Configuration.GetConnectionString("GameDb")!;

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddHsts(options =>
{
    options.Preload = true;
    options.IncludeSubDomains = true;
    options.MaxAge = TimeSpan.FromDays(365);
});

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status308PermanentRedirect;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "local",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = "__Host-MitharaAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<WebDbContext>(options =>
    options.UseNpgsql(gameConnStr));

builder.Services.AddSingleton(new GameDbService(gameConnStr));
builder.Services.AddScoped<ForumService>();
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<WikiService>(sp => new WikiService(sp.GetRequiredService<WebDbContext>(), gameConnStr));
builder.Services.AddScoped<MarketplaceService>(_ => new MarketplaceService(gameConnStr));

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WebDbContext>();
    var forumService = scope.ServiceProvider.GetRequiredService<ForumService>();
    var storeService = scope.ServiceProvider.GetRequiredService<StoreService>();
    var wikiService = scope.ServiceProvider.GetRequiredService<WikiService>();
    await db.EnsureTablesCreatedAsync();
    await forumService.EnsureDefaultCategoriesAsync();
    await storeService.EnsureDefaultProductsAsync();
    await wikiService.EnsureDefaultDataAsync();
    await wikiService.EnsureMobImagesAsync();
    var gameDb = app.Services.GetRequiredService<GameDbService>();
    gameDb.EnsureAdminColumn();
}

Console.WriteLine($"Mithara Online - Site rodando em {app.Urls.FirstOrDefault() ?? "http://localhost:5000"}");
app.Run();
