using Alisflyt.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Alisflyt.Infrastructure.Persistence.Configurations
{
    public class GrantRateSetConfiguration
        : IEntityTypeConfiguration<GrantRateSet>
    {
        public void Configure(EntityTypeBuilder<GrantRateSet> builder)
        {
            builder.ToTable("GrantRateSets");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.ValidFrom)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(x => x.ValidTo)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(x => x.SalaryRate)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.PracticeCompensationRate)
                .HasPrecision(8, 4)
                .IsRequired();

            builder.Property(x => x.PracticeCompensationMaxHours)
                .HasPrecision(8, 2)
                .IsRequired();

            builder.Property(x => x.LearningActivitiesMaxAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.ProductivityMaxAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.GuidanceRate)
                .HasPrecision(8, 4)
                .IsRequired();

            builder.Property(x => x.GuidanceHoursPerYear)
                .HasPrecision(8, 2)
                .IsRequired();

            builder.Property(x => x.FacilitationRate)
                .HasPrecision(8, 4)
                .IsRequired();

            builder.Property(x => x.CentralitySupplementMaxAmount)
                .HasPrecision(18, 2)
                .IsRequired();

            builder.Property(x => x.IsActive)
                .IsRequired();

            builder.Property(x => x.CreatedAtUtc)
                .IsRequired();

            builder.Property(x => x.LastModifiedAtUtc)
                .IsRequired();

            builder.HasIndex(x => new
            {
                x.ValidFrom,
                x.ValidTo
            });
        }
    }
}