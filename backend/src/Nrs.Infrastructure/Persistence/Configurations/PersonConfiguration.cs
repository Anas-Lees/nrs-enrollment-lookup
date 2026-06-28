using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent mapping for the PERSON hub table.
/// Arabic name columns are Unicode (IsUnicode(true) → NVARCHAR2 on Oracle);
/// ASCII-only columns are non-Unicode. Search columns are indexed.
/// </summary>
public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("PERSON");

        builder.HasKey(p => p.CivilNumber);

        builder.Property(p => p.CivilNumber)
            .HasColumnName("CIVIL_NUMBER")
            .HasMaxLength(9)
            .IsUnicode(false);

        builder.Property(p => p.FirstNameAr)
            .HasColumnName("FIRST_NAME_AR")
            .HasMaxLength(100)
            .IsUnicode(true);

        builder.Property(p => p.FamilyNameAr)
            .HasColumnName("FAMILY_NAME_AR")
            .HasMaxLength(100)
            .IsUnicode(true);

        builder.Property(p => p.FirstNameEn)
            .HasColumnName("FIRST_NAME_EN")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(p => p.FamilyNameEn)
            .HasColumnName("FAMILY_NAME_EN")
            .HasMaxLength(100)
            .IsUnicode(false);

        builder.Property(p => p.DateOfBirth)
            .HasColumnName("DATE_OF_BIRTH");

        builder.Property(p => p.Gender)
            .HasColumnName("GENDER")
            .HasMaxLength(1)
            .IsFixedLength()
            .IsUnicode(false);

        builder.Property(p => p.NationalityCode)
            .HasColumnName("NATIONALITY_CODE")
            .HasMaxLength(3)
            .IsUnicode(false);

        builder.Property(p => p.Status)
            .HasColumnName("STATUS")
            .HasMaxLength(20)
            .IsUnicode(false)
            .HasConversion<string>();

        builder.Property(p => p.PhotoPath)
            .HasColumnName("PHOTO_PATH")
            .HasMaxLength(500)
            .IsUnicode(false);

        // One PERSON has many ID cards and many passports (cascade delete the documents).
        builder.HasMany(p => p.IdCards)
            .WithOne(c => c.Person)
            .HasForeignKey(c => c.CivilNumber)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Passports)
            .WithOne(p => p.Person)
            .HasForeignKey(p => p.CivilNumber)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes on the search columns (CRN is already indexed as the primary key).
        builder.HasIndex(p => p.FamilyNameEn).HasDatabaseName("IX_PERSON_FAMILY_NAME_EN");
        builder.HasIndex(p => p.FamilyNameAr).HasDatabaseName("IX_PERSON_FAMILY_NAME_AR");
        builder.HasIndex(p => p.DateOfBirth).HasDatabaseName("IX_PERSON_DATE_OF_BIRTH");
        builder.HasIndex(p => p.NationalityCode).HasDatabaseName("IX_PERSON_NATIONALITY_CODE");
    }
}
