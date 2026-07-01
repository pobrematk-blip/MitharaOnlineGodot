using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Mithara.Web.Data;
using Mithara.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var gameConnStr = builder.Configuration.GetConnectionString("GameDb")!;

builder.Services.AddControllersWithViews();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<WebDbContext>(options =>
    options.UseNpgsql(gameConnStr));

builder.Services.AddSingleton(new GameDbService(gameConnStr));
builder.Services.AddScoped<ForumService>();
builder.Services.AddScoped<StoreService>();
builder.Services.AddScoped<WikiService>(sp => new WikiService(sp.GetRequiredService<WebDbContext>(), gameConnStr));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
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
}

Console.WriteLine("Mithara Online - Site rodando em http://localhost:5000");
app.Run();
