using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent mapping for the PASSPORT table. The PERSON ↔ PASSPORT relationship is
/// configured from the Person side; here we map columns, the key, and the FK index.
/// </summary>
public class PassportConfiguration : IEntityTypeConfiguration<Passport>
{
    public void Configure(EntityTypeBuilder<Passport> builder)
    {
        builder.ToTable("PASSPORT");

        builder.HasKey(p => p.PassportId);

        builder.Property(p => p.PassportId)
            .HasColumnName("PASSPORT_ID")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.CivilNumber)
            .HasColumnName("CIVIL_NUMBER")
            .HasMaxLength(9)
            .IsUnicode(false);

        builder.Property(p => p.PassportNumber)
            .HasColumnName("PASSPORT_NUMBER")
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(p => p.PassportType)
            .HasColumnName("PASSPORT_TYPE")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>();

        builder.Property(p => p.IssueDate)
            .HasColumnName("ISSUE_DATE");

        builder.Property(p => p.ExpiryDate)
            .HasColumnName("EXPIRY_DATE");

        builder.Property(p => p.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>();

        builder.HasIndex(p => p.CivilNumber).HasDatabaseName("IX_PASSPORT_CIVIL_NUMBER");
    }
}
