using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent mapping for the ENROLLMENT table. Arabic name columns are Unicode
/// (IsUnicode(true) → NVARCHAR2 on Oracle); ASCII-only columns are non-Unicode.
/// The enum columns are persisted as their string names. There is deliberately no
/// FK to PERSON: an enrollment may be for a brand-new applicant with no CRN yet, so
/// <see cref="Enrollment.CivilNumber"/> is a plain (indexed) reference, not a relationship.
/// </summary>
public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("ENROLLMENT");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("ID");

        builder.Property(e => e.ReferenceNumber)
            .HasColumnName("REFERENCE_NUMBER")
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(e => e.CivilNumber)
            .HasColumnName("CIVIL_NUMBER")
            .HasMaxLength(9)
            .IsUnicode(false)
            .IsRequired(false);

        builder.Property(e => e.FirstNameEn)
            .HasColumnName("FIRST_NAME_EN")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(e => e.FamilyNameEn)
            .HasColumnName("FAMILY_NAME_EN")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(e => e.FirstNameAr)
            .HasColumnName("FIRST_NAME_AR")
            .HasMaxLength(100)
            .IsUnicode(true);

        builder.Property(e => e.FamilyNameAr)
            .HasColumnName("FAMILY_NAME_AR")
            .HasMaxLength(100)
            .IsUnicode(true);

        builder.Property(e => e.DateOfBirth)
            .HasColumnName("DATE_OF_BIRTH");

        builder.Property(e => e.NationalityCode)
            .HasColumnName("NATIONALITY_CODE")
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(e => e.Type)
            .HasColumnName("TYPE")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>();

        builder.Property(e => e.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>();

        builder.Property(e => e.Notes)
            .HasColumnName("NOTES")
            .HasMaxLength(1000)
            .IsUnicode(true)
            .IsRequired(false);

        builder.Property(e => e.CreatedBy)
            .HasColumnName("CREATED_BY")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("CREATED_AT_UTC");

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("UPDATED_AT_UTC");

        // Review workflow columns — all nullable (added to a populated table; Oracle
        // rejects mandatory columns there, see ORA-01758).
        builder.Property(e => e.ProcessInstanceKey)
            .HasColumnName("PROCESS_INSTANCE_KEY")
            .IsRequired(false);

        builder.Property(e => e.DecidedBy)
            .HasColumnName("DECIDED_BY")
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired(false);

        builder.Property(e => e.DecidedAtUtc)
            .HasColumnName("DECIDED_AT_UTC")
            .IsRequired(false);

        builder.Property(e => e.DecisionNotes)
            .HasColumnName("DECISION_NOTES")
            .HasMaxLength(1000)
            .IsUnicode(true)
            .IsRequired(false);

        builder.Property(e => e.EscalatedAtUtc)
            .HasColumnName("ESCALATED_AT_UTC")
            .IsRequired(false);

        builder.Property(e => e.ScreeningFlags)
            .HasColumnName("SCREENING_FLAGS")
            .HasMaxLength(200)
            .IsUnicode(false)
            .IsRequired(false);

        // Reference number is the human-facing unique handle for the application.
        builder.HasIndex(e => e.ReferenceNumber)
            .IsUnique()
            .HasDatabaseName("IX_ENROLLMENT_REFERENCE_NUMBER");

        // Queue views filter/sort by status and recency; continuing applications look up by CRN.
        builder.HasIndex(e => e.Status).HasDatabaseName("IX_ENROLLMENT_STATUS");
        builder.HasIndex(e => e.CreatedAtUtc).HasDatabaseName("IX_ENROLLMENT_CREATED_AT");
        builder.HasIndex(e => e.CivilNumber).HasDatabaseName("IX_ENROLLMENT_CIVIL_NUMBER");
    }
}
