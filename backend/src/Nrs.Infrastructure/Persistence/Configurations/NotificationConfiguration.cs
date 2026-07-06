using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent mapping for the NOTIFICATION table. Message bodies are bilingual, so the Arabic
/// column is Unicode (NVARCHAR2). The hot query is "unread for this recipient", hence the
/// composite index on (RECIPIENT, READ_AT_UTC).
/// </summary>
public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("NOTIFICATION");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .HasColumnName("ID");

        builder.Property(n => n.Recipient)
            .HasColumnName("RECIPIENT")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(n => n.Kind)
            .HasColumnName("KIND")
            .HasMaxLength(30)
            .IsUnicode(false);

        builder.Property(n => n.EnrollmentId)
            .HasColumnName("ENROLLMENT_ID")
            .IsRequired(false);

        builder.Property(n => n.ReferenceNumber)
            .HasColumnName("REFERENCE_NUMBER")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired(false);

        builder.Property(n => n.MessageEn)
            .HasColumnName("MESSAGE_EN")
            .HasMaxLength(500)
            .IsUnicode(false);

        builder.Property(n => n.MessageAr)
            .HasColumnName("MESSAGE_AR")
            .HasMaxLength(500)
            .IsUnicode(true);

        builder.Property(n => n.CreatedAtUtc)
            .HasColumnName("CREATED_AT_UTC");

        builder.Property(n => n.ReadAtUtc)
            .HasColumnName("READ_AT_UTC")
            .IsRequired(false);

        // The bell asks: "unread items for me (and my roles), newest first".
        builder.HasIndex(n => new { n.Recipient, n.ReadAtUtc })
            .HasDatabaseName("IX_NOTIFICATION_RECIPIENT_READ");
        builder.HasIndex(n => n.CreatedAtUtc)
            .HasDatabaseName("IX_NOTIFICATION_CREATED_AT");
    }
}
