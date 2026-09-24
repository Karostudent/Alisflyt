using System.Text.Json;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Alisflyt.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Alisflyt.IntegrationTests;

public class GrantApplicationPersistenceTests
{
    [Fact]
    public async Task Migration_and_fresh_context_preserve_both_form_steps()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer($"Server=(localdb)\\MSSQLLocalDB;Database=Alisflyt_FormTest_{Guid.NewGuid():N};Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        var id = Guid.NewGuid();
        var data = new GrantApplicationData { DoctorName = "Test Lege", HprNumber = "1234567",
            EmploymentPeriods = [new() { PositionPercentage = 80 }, new() { PositionPercentage = 20 }],
            Certificate = new() { SupervisorName = "Test Veileder", Sessions = [new() { Hours = 1.5m, Topic = "Tema" }] } };
        try
        {
            await using (var db = new ApplicationDbContext(options))
            {
                await db.Database.MigrateAsync();
                var entity = GrantCase.Create(id, "FORM-TEST", DateTimeOffset.UtcNow);
                entity.UpdateApplication(data, DateTimeOffset.UtcNow);
                db.Add(entity);
                await db.SaveChangesAsync();
            }
            await using (var db = new ApplicationDbContext(options))
            {
                var entity = await db.Set<GrantCase>().SingleAsync(c => c.Id == id);
                Assert.Equal(JsonSerializer.Serialize(data), entity.ApplicationDataJson);
                Assert.Equal("1234567", entity.HprNumber);
                Assert.Equal(80m, entity.EmploymentPercentage);
            }
        }
        finally
        {
            await using var db = new ApplicationDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
