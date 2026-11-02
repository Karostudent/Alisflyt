using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Alisflyt.Infrastructure.Identity;
using System.Security.Claims;
using Alisflyt.Application.Models;

var builder = WebApplication.CreateBuilder(args);

// ensure DefaultConnection is present in Development
if (builder.Environment.IsDevelopment())
{
    var cfg = builder.Configuration;
    var conn = cfg.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(conn))
        throw new InvalidOperationException("DefaultConnection is required in appsettings.Development.json for local development.");
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<GrantCaseAccessContext>(services =>
{
    var user = services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;
    return new GrantCaseAccessContext(
        user?.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        user?.FindFirstValue(ClaimTypes.Role) ?? string.Empty);
});
builder.Services
    .AddIdentity<AlisflytUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<Alisflyt.Infrastructure.Persistence.ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

// Infrastructure & Application DI
builder.Services.AddDbContext<Alisflyt.Infrastructure.Persistence.ApplicationDbContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<Alisflyt.Application.Abstractions.IGrantCaseRepository, Alisflyt.Infrastructure.Persistence.Repositories.GrantCaseRepository>();
builder.Services.AddScoped<Alisflyt.Application.Abstractions.IGrantRateSetRepository, Alisflyt.Infrastructure.Persistence.Repositories.GrantRateSetRepository>();
builder.Services.AddScoped<Alisflyt.Application.Abstractions.ICaseNumberGenerator, Alisflyt.Infrastructure.Services.CaseNumberGenerator>();
builder.Services.AddScoped<Alisflyt.Application.Services.IGrantCaseApplicationService, Alisflyt.Application.Services.GrantCaseApplicationService>();
builder.Services.AddScoped<Alisflyt.Application.Services.IGrantCalculationApplicationService, Alisflyt.Application.Services.GrantCalculationApplicationService>();
builder.Services.AddScoped<Alisflyt.Application.Services.IGuidanceApprovalService, Alisflyt.Application.Services.GuidanceApprovalService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Alisflyt.Infrastructure.Development.DevelopmentDataSeeder>();

var app = builder.Build();

// Run automatic migrations and seed in Development only
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var cfg = services.GetRequiredService<IConfiguration>();

    var conn = cfg.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(conn))
        throw new InvalidOperationException("DefaultConnection is required in appsettings.Development.json for local development.");

    var db = services.GetRequiredService<Alisflyt.Infrastructure.Persistence.ApplicationDbContext>();
    db.Database.Migrate();

    if (bool.TryParse(cfg["SeedDemoData"], out var seed) && seed)
    {
        Alisflyt.Infrastructure.Development.DevelopmentDataSeeder.SeedAsync(db, TimeProvider.System).GetAwaiter().GetResult();
    }

}

using (var scope = app.Services.CreateScope())
{
    SeedIdentityAsync(scope.ServiceProvider, app.Configuration).GetAwaiter().GetResult();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "portal",
    pattern: "portal",
    defaults: new { controller = "Portal", action = "Index" });

app.Run();

static async Task SeedIdentityAsync(IServiceProvider services, IConfiguration configuration)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var roles = new[] { "ALIS", "Veileder", "Avdelingsleder", "Administrator" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not create Identity role '{role}': {string.Join(", ", result.Errors.Select(error => error.Description))}");
        }
    }

    var username = configuration["Identity:BootstrapAdmin:UserName"];
    var email = configuration["Identity:BootstrapAdmin:Email"];
    var password = configuration["Identity:BootstrapAdmin:Password"];
    if (string.IsNullOrWhiteSpace(username) && string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password))
        return;
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        throw new InvalidOperationException("Identity bootstrap requires UserName, Email and Password.");

    var userManager = services.GetRequiredService<UserManager<AlisflytUser>>();
    var user = await userManager.FindByNameAsync(username);
    if (user is null)
    {
        user = new AlisflytUser { UserName = username, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not create bootstrap administrator: {string.Join(", ", result.Errors.Select(error => error.Description))}");
    }

    if (!await userManager.IsInRoleAsync(user, "Administrator"))
    {
        var result = await userManager.AddToRoleAsync(user, "Administrator");
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not assign Administrator role: {string.Join(", ", result.Errors.Select(error => error.Description))}");
    }
}

public partial class Program { }