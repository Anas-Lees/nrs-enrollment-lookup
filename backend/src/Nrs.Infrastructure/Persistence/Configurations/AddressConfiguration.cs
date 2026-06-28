using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>Fluent mapping for the ADDRESS table (one-to-one with PERSON via the CRN).</summary>
public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("ADDRESS");

        builder.HasKey(a => a.CivilNumber);

        builder.Property(a => a.CivilNumber).HasColumnName("CIVIL_NUMBER").HasMaxLength(9).IsUnicode(false);
        builder.Property(a => a.Governorate).HasColumnName("GOVERNORATE").HasMaxLength(50).IsUnicode(false);
        builder.Property(a => a.Wilayat).HasColumnName("WILAYAT").HasMaxLength(50).IsUnicode(false);
        builder.Property(a => a.Village).HasColumnName("VILLAGE").HasMaxLength(80).IsUnicode(false);
        builder.Property(a => a.Street).HasColumnName("STREET").HasMaxLength(120).IsUnicode(false);
        builder.Property(a => a.BuildingNumber).HasColumnName("BUILDING_NO").HasMaxLength(20).IsUnicode(false);
        builder.Property(a => a.PostalCode).HasColumnName("POSTAL_CODE").HasMaxLength(10).IsUnicode(false);
    }
}
