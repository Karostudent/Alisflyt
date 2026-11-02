using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Alisflyt.Domain.Entities;
using Alisflyt.Infrastructure.Identity;

namespace Alisflyt.Infrastructure.Persistence
{
    public class ApplicationDbContext : IdentityDbContext<AlisflytUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<GrantCase> GrantCases => Set<GrantCase>();
        public DbSet<GrantRateSet> GrantRateSets => Set<GrantRateSet>();
        public DbSet<GuidanceApproval> GuidanceApprovals => Set<GuidanceApproval>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AlisflytUser>(b =>
            {
                b.Property(x => x.HprNumber).HasMaxLength(50);
                b.HasIndex(x => x.HprNumber).IsUnique().HasFilter("[HprNumber] IS NOT NULL");
            });

            modelBuilder.Entity<GrantCase>(b =>
            {
                b.Property(x => x.ReadyForAuditorApprovalAtUtc)
                    .HasColumnType("datetimeoffset");
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
