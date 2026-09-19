using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

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

// Infrastructure & Application DI
builder.Services.AddDbContext<Alisflyt.Infrastructure.Persistence.ApplicationDbContext>((sp, options) =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<Alisflyt.Application.Abstractions.IGrantCaseRepository, Alisflyt.Infrastructure.Persistence.Repositories.GrantCaseRepository>();
builder.Services.AddScoped<Alisflyt.Application.Abstractions.ICaseNumberGenerator, Alisflyt.Infrastructure.Services.CaseNumberGenerator>();
builder.Services.AddScoped<Alisflyt.Application.Services.IGrantCaseApplicationService, Alisflyt.Application.Services.GrantCaseApplicationService>();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<Alisflyt.Infrastructure.Development.DevelopmentDataSeeder>();

var app = builder.Build();

// Run automatic migrations and seed in Development only
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var cfg = services.GetRequiredService<IConfiguration>();
    var env = services.GetRequiredService<IHostEnvironment>();

    var conn = cfg.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(conn))
        throw new InvalidOperationException("DefaultConnection is required in appsettings.Development.json for local development.");

    var db = services.GetRequiredService<Alisflyt.Infrastructure.Persistence.ApplicationDbContext>();
    // migrate
    db.Database.Migrate();

    // seed if enabled
    if (bool.TryParse(cfg["SeedDemoData"], out var seed) && seed)
    {
        // DevelopmentDataSeeder exposes a static SeedAsync method
        Alisflyt.Infrastructure.Development.DevelopmentDataSeeder.SeedAsync(db, TimeProvider.System).GetAwaiter().GetResult();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Portal}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
