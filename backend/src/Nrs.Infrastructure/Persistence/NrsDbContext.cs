using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence;

/// <summary>
/// EF Core database context — the gateway to the NRS database. Provider-agnostic:
/// the concrete provider (SQLite for local dev, Oracle for higher environments) is
/// chosen at the composition root via configuration.
/// </summary>
public class NrsDbContext(DbContextOptions<NrsDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons => Set<Person>();

    public DbSet<IdCard> IdCards => Set<IdCard>();

    public DbSet<Passport> Passports => Set<Passport>();

    public DbSet<Nationality> Nationalities => Set<Nationality>();

    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply every IEntityTypeConfiguration in this assembly (Person/IdCard/Passport).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NrsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
