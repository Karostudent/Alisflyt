using Microsoft.EntityFrameworkCore;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<GrantCase> GrantCases => Set<GrantCase>();
        public DbSet<GrantRateSet> GrantRateSets => Set<GrantRateSet>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
