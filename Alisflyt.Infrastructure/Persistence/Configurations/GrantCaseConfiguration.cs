using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Alisflyt.Domain.Entities;

namespace Alisflyt.Infrastructure.Persistence.Configurations
{
    public class GrantCaseConfiguration : IEntityTypeConfiguration<GrantCase>
    {
        public void Configure(EntityTypeBuilder<GrantCase> builder)
        {
            builder.ToTable("GrantCases");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.CaseNumber)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(c => c.CaseNumber).IsUnique();

            builder.Property(c => c.HprNumber)
                .HasMaxLength(50);

            builder.Property(c => c.EmploymentPercentage)
                .HasPrecision(5, 2);

            builder.Property(c => c.EmploymentStartDate)
                .HasColumnType("date");

            builder.Property(c => c.EmploymentEndDate)
                .HasColumnType("date");

            builder.Property(c => c.Status)
                .IsRequired();

            builder.Property(c => c.CreatedAtUtc)
                .IsRequired();

            builder.Property(c => c.LastModifiedAtUtc)
                .IsRequired();
        }
    }
}
