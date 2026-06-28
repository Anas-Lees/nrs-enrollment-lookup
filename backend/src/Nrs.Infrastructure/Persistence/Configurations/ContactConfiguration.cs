using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence.Configurations;

/// <summary>Fluent mapping for the CONTACT table (one-to-one with PERSON via the CRN).</summary>
public class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("CONTACT");

        builder.HasKey(c => c.CivilNumber);

        builder.Property(c => c.CivilNumber).HasColumnName("CIVIL_NUMBER").HasMaxLength(9).IsUnicode(false);
        builder.Property(c => c.Mobile).HasColumnName("MOBILE").HasMaxLength(20).IsUnicode(false);
        builder.Property(c => c.Email).HasColumnName("EMAIL").HasMaxLength(120).IsUnicode(false);
    }
}
