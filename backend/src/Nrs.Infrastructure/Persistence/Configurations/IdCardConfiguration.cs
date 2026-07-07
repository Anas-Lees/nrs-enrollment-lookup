using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent mapping for the ID_CARD table. The PERSON ↔ ID_CARD relationship is
/// configured from the Person side; here we map columns, the key, and the FK index.
/// </summary>
public class IdCardConfiguration : IEntityTypeConfiguration<IdCard>
{
    public void Configure(EntityTypeBuilder<IdCard> builder)
    {
        builder.ToTable("ID_CARD");

        builder.HasKey(c => c.IdCardId);

        builder.Property(c => c.IdCardId)
            .HasColumnName("ID_CARD_ID")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.CivilNumber)
            .HasColumnName("CIVIL_NUMBER")
            .HasMaxLength(9)
            .IsUnicode(false);

        builder.Property(c => c.CardNumber)
            .HasColumnName("CARD_NUMBER")
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(c => c.IssueDate)
            .HasColumnName("ISSUE_DATE");

        builder.Property(c => c.ExpiryDate)
            .HasColumnName("EXPIRY_DATE");

        builder.Property(c => c.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>();

        builder.Property(c => c.CardType)
            .HasColumnName("CARD_TYPE")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>();

        builder.Property(c => c.EnrollmentId)
            .HasColumnName("ENROLLMENT_ID")
            .IsRequired(false);

        builder.HasIndex(c => c.CivilNumber).HasDatabaseName("IX_ID_CARD_CIVIL_NUMBER");

        // The card office lists cards by production status; keep that lookup cheap.
        builder.HasIndex(c => c.Status).HasDatabaseName("IX_ID_CARD_STATUS");

        // One card per enrollment — the guard behind provisioning's idempotency. Oracle does not
        // index all-null keys, so the many seeded cards (null EnrollmentId) are unaffected; a
        // concurrent second provision for the same enrollment fails fast and self-heals on retry.
        builder.HasIndex(c => c.EnrollmentId)
            .IsUnique()
            .HasFilter(null) // Oracle already excludes NULL keys; no SQL-Server-style filtered index.
            .HasDatabaseName("UX_ID_CARD_ENROLLMENT");
    }
}
