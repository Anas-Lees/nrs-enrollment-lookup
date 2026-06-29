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

        // Normalized search column across all four name parts. Unicode (→ NVARCHAR2 on
        // Oracle) because it holds folded Arabic; long enough for 4×100 + separators.
        // Nullable so it adds cleanly to an existing table (then the seeder backfills it),
        // and because Oracle represents an empty string as NULL anyway.
        builder.Property(p => p.NameSearch)
            .HasColumnName("NAME_SEARCH")
            .HasMaxLength(420)
            .IsUnicode(true)
            .IsRequired(false);

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

        // Extended biographic columns (Arabic ones are Unicode → NVARCHAR2 on Oracle).
        builder.Property(p => p.PlaceOfBirthEn).HasColumnName("PLACE_OF_BIRTH_EN").HasMaxLength(100).IsUnicode(false);
        builder.Property(p => p.PlaceOfBirthAr).HasColumnName("PLACE_OF_BIRTH_AR").HasMaxLength(100).IsUnicode(true);
        builder.Property(p => p.MotherNameEn).HasColumnName("MOTHER_NAME_EN").HasMaxLength(100).IsUnicode(false);
        builder.Property(p => p.MotherNameAr).HasColumnName("MOTHER_NAME_AR").HasMaxLength(100).IsUnicode(true);
        builder.Property(p => p.MaritalStatus).HasColumnName("MARITAL_STATUS").HasMaxLength(20).IsUnicode(false).HasConversion<string>();
        builder.Property(p => p.BloodType).HasColumnName("BLOOD_TYPE").HasMaxLength(3).IsUnicode(false);
        builder.Property(p => p.OccupationEn).HasColumnName("OCCUPATION_EN").HasMaxLength(100).IsUnicode(false);
        builder.Property(p => p.OccupationAr).HasColumnName("OCCUPATION_AR").HasMaxLength(100).IsUnicode(true);

        // Nationality lookup (FK by code; don't cascade — it's reference data).
        builder.HasOne(p => p.Nationality)
            .WithMany(n => n.Persons)
            .HasForeignKey(p => p.NationalityCode)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-one address and contact, sharing the CRN; cascade with the person.
        builder.HasOne(p => p.Address)
            .WithOne(a => a.Person)
            .HasForeignKey<Address>(a => a.CivilNumber)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Contact)
            .WithOne(c => c.Person)
            .HasForeignKey<Contact>(c => c.CivilNumber)
            .OnDelete(DeleteBehavior.Cascade);

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
        builder.HasIndex(p => p.NameSearch).HasDatabaseName("IX_PERSON_NAME_SEARCH");
        builder.HasIndex(p => p.DateOfBirth).HasDatabaseName("IX_PERSON_DATE_OF_BIRTH");
        builder.HasIndex(p => p.NationalityCode).HasDatabaseName("IX_PERSON_NATIONALITY_CODE");
    }
}
