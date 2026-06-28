using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent mapping for the NATIONALITY lookup table, seeded with reference data via
/// HasData (so it ships in the migration, not the sample-data seeder).
/// </summary>
public class NationalityConfiguration : IEntityTypeConfiguration<Nationality>
{
    public void Configure(EntityTypeBuilder<Nationality> builder)
    {
        builder.ToTable("NATIONALITY");

        builder.HasKey(n => n.Code);

        builder.Property(n => n.Code).HasColumnName("CODE").HasMaxLength(3).IsUnicode(false);
        builder.Property(n => n.NameEn).HasColumnName("NAME_EN").HasMaxLength(100).IsUnicode(false);
        builder.Property(n => n.NameAr).HasColumnName("NAME_AR").HasMaxLength(100).IsUnicode(true);

        builder.HasData(
            new Nationality { Code = "OMN", NameEn = "Oman", NameAr = "عُمان" },
            new Nationality { Code = "ARE", NameEn = "United Arab Emirates", NameAr = "الإمارات" },
            new Nationality { Code = "SAU", NameEn = "Saudi Arabia", NameAr = "السعودية" },
            new Nationality { Code = "KWT", NameEn = "Kuwait", NameAr = "الكويت" },
            new Nationality { Code = "QAT", NameEn = "Qatar", NameAr = "قطر" },
            new Nationality { Code = "BHR", NameEn = "Bahrain", NameAr = "البحرين" },
            new Nationality { Code = "YEM", NameEn = "Yemen", NameAr = "اليمن" },
            new Nationality { Code = "JOR", NameEn = "Jordan", NameAr = "الأردن" },
            new Nationality { Code = "EGY", NameEn = "Egypt", NameAr = "مصر" },
            new Nationality { Code = "IND", NameEn = "India", NameAr = "الهند" },
            new Nationality { Code = "PAK", NameEn = "Pakistan", NameAr = "باكستان" },
            new Nationality { Code = "BGD", NameEn = "Bangladesh", NameAr = "بنغلاديش" },
            new Nationality { Code = "PHL", NameEn = "Philippines", NameAr = "الفلبين" },
            new Nationality { Code = "LKA", NameEn = "Sri Lanka", NameAr = "سريلانكا" },
            new Nationality { Code = "GBR", NameEn = "United Kingdom", NameAr = "المملكة المتحدة" },
            new Nationality { Code = "USA", NameEn = "United States", NameAr = "الولايات المتحدة" });
    }
}
