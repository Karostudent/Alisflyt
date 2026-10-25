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
        public DbSet<GuidanceApproval> GuidanceApprovals => Set<GuidanceApproval>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<GrantCase>(b =>
            {
                b.HasMany(g => g.GuidanceApprovals)
                    .WithOne(a => a.GrantCase)
                    .HasForeignKey(a => a.GrantCaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<GuidanceApproval>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.DoctorHprNumber).IsRequired().HasMaxLength(50);
                b.Property(x => x.SupervisorHprNumber).IsRequired().HasMaxLength(50);
                b.Property(x => x.SupervisorUserKey).IsRequired().HasMaxLength(100);
                b.Property(x => x.SupervisorName).HasMaxLength(200);
                b.Property(x => x.ApprovedBy).HasMaxLength(100);
                b.Property(x => x.RequestedRevisionReason).HasMaxLength(1000);
                b.Property(x => x.VersionNumber).IsRequired();
                b.Property(x => x.AttestationSnapshotHash).IsRequired().HasMaxLength(128);
                b.Property(x => x.AttestationSnapshotJson).HasColumnType("nvarchar(max)");
                b.Property(x => x.CreatedAtUtc).IsRequired();
                b.Property(x => x.UpdatedAtUtc).IsRequired();
                b.HasIndex(x => x.GrantCaseId);
                b.HasIndex(x => new { x.GrantCaseId, x.VersionNumber });
                b.HasIndex(x => x.Status);
            });

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
