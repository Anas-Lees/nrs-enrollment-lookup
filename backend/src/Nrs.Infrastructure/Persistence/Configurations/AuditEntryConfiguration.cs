using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>Fluent mapping for the append-only AUDIT_ENTRY table.</summary>
public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AUDIT_ENTRY");

        builder.HasKey(a => a.AuditId);
        builder.Property(a => a.AuditId).HasColumnName("AUDIT_ID").ValueGeneratedOnAdd();

        builder.Property(a => a.TimestampUtc).HasColumnName("TIMESTAMP_UTC");
        builder.Property(a => a.Actor).HasColumnName("ACTOR").HasMaxLength(100).IsUnicode(false);
        builder.Property(a => a.Action).HasColumnName("ACTION").HasMaxLength(20).IsUnicode(false).HasConversion<string>();
        builder.Property(a => a.TargetCrn).HasColumnName("TARGET_CRN").HasMaxLength(9).IsUnicode(false);
        // Criteria may include Arabic name fragments → Unicode (NVARCHAR2 on Oracle).
        builder.Property(a => a.Criteria).HasColumnName("CRITERIA").HasMaxLength(400).IsUnicode(true);
        builder.Property(a => a.ResultCount).HasColumnName("RESULT_COUNT");
        builder.Property(a => a.SourceIp).HasColumnName("SOURCE_IP").HasMaxLength(45).IsUnicode(false);

        builder.HasIndex(a => a.TimestampUtc).HasDatabaseName("IX_AUDIT_ENTRY_TIMESTAMP");
        builder.HasIndex(a => a.Actor).HasDatabaseName("IX_AUDIT_ENTRY_ACTOR");
    }
}
