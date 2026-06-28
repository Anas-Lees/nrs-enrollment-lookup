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

        builder.HasIndex(c => c.CivilNumber).HasDatabaseName("IX_ID_CARD_CIVIL_NUMBER");
    }
}
