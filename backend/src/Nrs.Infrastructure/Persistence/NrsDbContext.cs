using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;

namespace Nrs.Infrastructure.Persistence;

/// <summary>
/// EF Core database context — the gateway to the NRS Oracle database. The connection is
/// configured at the composition root; the single migration set lives in this assembly.
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

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply every IEntityTypeConfiguration in this assembly (Person/IdCard/Passport).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NrsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
