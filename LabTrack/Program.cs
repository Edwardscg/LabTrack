using System.Globalization;
using LabTrack.Data;
using LabTrack.Models;
using LabTrack.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<LabTrackDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddIdentity<Usuario, IdentityRole>(options =>
{
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
        Math.Clamp(builder.Configuration.GetValue("Security:LockoutMinutes", 15), 1, 1440));
    options.Lockout.AllowedForNewUsers = true;
    options.Password.RequiredLength = 10;
}).AddEntityFrameworkStores<LabTrackDbContext>().AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "LabTrack.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization(options => options.FallbackPolicy =
    new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddSingleton<LabClock>();

var app = builder.Build();
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("es-PE");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-PE");
if (args.Contains("--initialize"))
{
    using var scope = app.Services.CreateScope();
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration);
    app.Logger.LogInformation("Base de datos y datos iniciales preparados.");
    return;
}
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
app.Run();
