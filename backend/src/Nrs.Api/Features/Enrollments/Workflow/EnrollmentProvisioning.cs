using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Nrs.Application.Search;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

namespace Nrs.Api.Features.Enrollments.Workflow;

/// <summary>
/// Turns an approved enrollment into real registry data — the moment the paperwork becomes a
/// person and a card. A brand-new applicant is registered as a <see cref="Person"/> with a
/// freshly minted CRN (written back onto the enrollment); a continuing applicant keeps their
/// record (a correction updates the registry name). Either way a new ID card is issued in
/// <see cref="CardStatus.IN_PRODUCTION"/> and handed to the card-office fulfilment steps.
/// Idempotent: re-running (Camunda delivers at-least-once) issues no second card — the check is
/// backed by a unique index on ID_CARD.ENROLLMENT_ID so a concurrent second provision fails fast
/// and the retry then finds the existing card.
/// </summary>
public static class EnrollmentProvisioning
{
    /// <summary>Provisions the registry record + card, returning the CRN the card was issued under.</summary>
    public static async Task<string> ProvisionAsync(NrsDbContext db, Enrollment enrollment, CancellationToken ct)
    {
        // Already provisioned? (idempotent re-delivery) — leave everything as it is.
        var existing = await db.IdCards.AsNoTracking()
            .FirstOrDefaultAsync(c => c.EnrollmentId == enrollment.Id, ct);
        if (existing is not null)
        {
            return existing.CivilNumber;
        }

        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        string crn;

        if (string.IsNullOrWhiteSpace(enrollment.CivilNumber))
        {
            // Brand-new applicant → register them as a person with a fresh CRN.
            crn = await GenerateUniqueCrnAsync(db, ct);
            db.Persons.Add(BuildPerson(enrollment, crn, today));

            // Link the application to the person it created, so the detail page can open the
            // profile and screening no longer treats it as an unknown CRN.
            enrollment.CivilNumber = crn;
            enrollment.UpdatedAtUtc = now;
        }
        else
        {
            crn = enrollment.CivilNumber;
            var person = await db.Persons
                .Include(p => p.Address)
                .Include(p => p.Contact)
                .Include(p => p.Passports)
                .FirstOrDefaultAsync(p => p.CivilNumber == crn, ct);
            if (person is null)
            {
                // The referenced record does not exist — the reviewer approved despite a
                // CRN-not-found screening flag. Register the applicant under that CRN now rather
                // than issue a card against a non-existent person.
                db.Persons.Add(BuildPerson(enrollment, crn, today));
            }
            else
            {
                // The operator re-captured the applicant's details at the counter, so refresh the
                // registry with them (a correction also rewrites the name).
                RefreshExistingPerson(person, enrollment, today);
            }
        }

        db.IdCards.Add(new IdCard
        {
            CivilNumber = crn,
            CardNumber = GenerateCardNumber(),
            IssueDate = today,
            ExpiryDate = today.AddYears(10),
            CardType = enrollment.NationalityCode == "OMN" ? CardType.OMANI : CardType.RESIDENT,
            Status = CardStatus.IN_PRODUCTION,
            EnrollmentId = enrollment.Id,
        });

        await db.SaveChangesAsync(ct);
        return crn;
    }

    /// <summary>
    /// Activates the card this enrollment produced (once collected) and supersedes the holder's
    /// previous live cards — a renewal/replacement makes the old card EXPIRED. Returns true only if
    /// it actually activated the card (idempotent; the caller notifies only on a real change). Does
    /// not save — the caller commits the status change together with its notification.
    /// </summary>
    public static async Task<bool> ActivateCardAsync(NrsDbContext db, Enrollment enrollment, CancellationToken ct)
    {
        var card = await db.IdCards.FirstOrDefaultAsync(c => c.EnrollmentId == enrollment.Id, ct);
        if (card is null || card.Status == CardStatus.ACTIVE)
        {
            return false;
        }

        // The old card stays valid right up until the new one is collected, so supersede only now.
        var superseded = await db.IdCards
            .Where(c => c.CivilNumber == card.CivilNumber
                        && c.IdCardId != card.IdCardId
                        && c.Status == CardStatus.ACTIVE)
            .ToListAsync(ct);
        foreach (var old in superseded)
        {
            old.Status = CardStatus.EXPIRED;
        }

        card.Status = CardStatus.ACTIVE;
        return true;
    }

    /// <summary>
    /// Moves the enrollment's card between production states. Returns true only if the status
    /// actually changed (guarded + idempotent); does not save — the caller commits it with its
    /// notification, so a re-delivered or degraded call cannot double-notify.
    /// </summary>
    public static async Task<bool> SetCardStatusAsync(
        NrsDbContext db, Enrollment enrollment, CardStatus from, CardStatus to, CancellationToken ct)
    {
        var card = await db.IdCards.FirstOrDefaultAsync(c => c.EnrollmentId == enrollment.Id, ct);
        if (card is null || card.Status != from)
        {
            return false;
        }

        card.Status = to;
        return true;
    }

    private static Person BuildPerson(Enrollment e, string crn, DateOnly today)
    {
        var person = new Person
        {
            CivilNumber = crn,
            FirstNameEn = e.FirstNameEn,
            FamilyNameEn = e.FamilyNameEn,
            FirstNameAr = e.FirstNameAr,
            FamilyNameAr = e.FamilyNameAr,
            NameSearch = BuildNameSearch(e),
            DateOfBirth = e.DateOfBirth,
            Gender = string.IsNullOrWhiteSpace(e.Gender) ? "M" : e.Gender!,
            NationalityCode = e.NationalityCode,
            Status = PersonStatus.ACTIVE,
            // Full biographic record captured at enrollment — so the profile isn't a shell.
            PlaceOfBirthEn = e.PlaceOfBirthEn,
            PlaceOfBirthAr = e.PlaceOfBirthAr,
            MotherNameEn = e.MotherNameEn,
            MotherNameAr = e.MotherNameAr,
            MaritalStatus = e.MaritalStatus,
            BloodType = e.BloodType,
            OccupationEn = e.OccupationEn,
            OccupationAr = e.OccupationAr,
            PhotoPath = $"/photos/{crn}.jpg",
        };

        // Address and contact share the CRN; attaching them inserts the rows with the person.
        if (BuildAddress(e, crn) is { } address)
        {
            person.Address = address;
        }
        if (BuildContact(e, crn) is { } contact)
        {
            person.Contact = contact;
        }
        if (BuildPassport(e, crn, today) is { } passport)
        {
            person.Passports.Add(passport);
        }

        return person;
    }

    /// <summary>
    /// Refreshes an already-registered person from the freshly-captured enrollment: a correction
    /// rewrites the name; the biographic fields, address and contact are updated to the values the
    /// operator just verified (only overwriting where the enrollment supplies a value, so an
    /// unfilled optional never blanks an existing one); a newly-supplied passport is added.
    /// </summary>
    private static void RefreshExistingPerson(Person person, Enrollment e, DateOnly today)
    {
        if (e.Type == EnrollmentType.CORRECTION)
        {
            person.FirstNameEn = e.FirstNameEn;
            person.FamilyNameEn = e.FamilyNameEn;
            person.FirstNameAr = e.FirstNameAr;
            person.FamilyNameAr = e.FamilyNameAr;
            person.NameSearch = BuildNameSearch(e);
        }

        person.PlaceOfBirthEn = e.PlaceOfBirthEn ?? person.PlaceOfBirthEn;
        person.PlaceOfBirthAr = e.PlaceOfBirthAr ?? person.PlaceOfBirthAr;
        person.MotherNameEn = e.MotherNameEn ?? person.MotherNameEn;
        person.MotherNameAr = e.MotherNameAr ?? person.MotherNameAr;
        person.MaritalStatus = e.MaritalStatus ?? person.MaritalStatus;
        person.BloodType = e.BloodType ?? person.BloodType;
        person.OccupationEn = e.OccupationEn ?? person.OccupationEn;
        person.OccupationAr = e.OccupationAr ?? person.OccupationAr;

        if (!string.IsNullOrWhiteSpace(e.Governorate))
        {
            person.Address ??= new Address { CivilNumber = person.CivilNumber };
            person.Address.Governorate = e.Governorate!;
            person.Address.Wilayat = e.Wilayat ?? person.Address.Wilayat;
            person.Address.Village = e.Village ?? person.Address.Village;
            person.Address.Street = e.Street ?? person.Address.Street;
            person.Address.BuildingNumber = e.BuildingNumber ?? person.Address.BuildingNumber;
            person.Address.PostalCode = e.PostalCode ?? person.Address.PostalCode;
        }

        if (!string.IsNullOrWhiteSpace(e.Mobile) || !string.IsNullOrWhiteSpace(e.Email))
        {
            person.Contact ??= new Contact { CivilNumber = person.CivilNumber };
            person.Contact.Mobile = e.Mobile ?? person.Contact.Mobile;
            person.Contact.Email = e.Email ?? person.Contact.Email;
        }

        // Add the captured passport if it's new to this person.
        if (!string.IsNullOrWhiteSpace(e.PassportNumber)
            && !person.Passports.Any(p => p.PassportNumber == e.PassportNumber)
            && BuildPassport(e, person.CivilNumber, today) is { } passport)
        {
            person.Passports.Add(passport);
        }
    }

    /// <summary>An address row from the enrollment, or null when no governorate was captured.</summary>
    private static Address? BuildAddress(Enrollment e, string crn) =>
        string.IsNullOrWhiteSpace(e.Governorate)
            ? null
            : new Address
            {
                CivilNumber = crn,
                Governorate = e.Governorate!,
                Wilayat = e.Wilayat ?? string.Empty,
                Village = e.Village,
                Street = e.Street,
                BuildingNumber = e.BuildingNumber,
                PostalCode = e.PostalCode,
            };

    /// <summary>A contact row from the enrollment, or null when neither mobile nor email was given.</summary>
    private static Contact? BuildContact(Enrollment e, string crn) =>
        string.IsNullOrWhiteSpace(e.Mobile) && string.IsNullOrWhiteSpace(e.Email)
            ? null
            : new Contact { CivilNumber = crn, Mobile = e.Mobile, Email = e.Email };

    /// <summary>A passport row from the enrollment, or null when none was captured.</summary>
    private static Passport? BuildPassport(Enrollment e, string crn, DateOnly today) =>
        string.IsNullOrWhiteSpace(e.PassportNumber)
            ? null
            : new Passport
            {
                CivilNumber = crn,
                PassportNumber = e.PassportNumber!,
                PassportType = e.PassportType ?? Nrs.Domain.Enums.PassportType.ORDINARY,
                IssueDate = e.PassportIssueDate,
                ExpiryDate = e.PassportExpiryDate,
                Status = e.PassportExpiryDate is { } exp && exp < today
                    ? PassportStatus.EXPIRED
                    : PassportStatus.ACTIVE,
            };

    private static string BuildNameSearch(Enrollment e)
    {
        var parts = new[]
        {
            NameNormalizer.Normalize(e.FirstNameAr),
            NameNormalizer.Normalize(e.FamilyNameAr),
            NameNormalizer.Normalize(e.FirstNameEn),
            NameNormalizer.Normalize(e.FamilyNameEn),
        };
        return string.Join(" ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }

    private static async Task<string> GenerateUniqueCrnAsync(NrsDbContext db, CancellationToken ct)
    {
        // 8-digit CRN (fits CIVIL_NUMBER(9)); retry on the rare collision with an existing record.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var crn = Random.Shared.Next(10_000_000, 100_000_000).ToString(CultureInfo.InvariantCulture);
            if (!await db.Persons.AsNoTracking().AnyAsync(p => p.CivilNumber == crn, ct))
            {
                return crn;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique civil number after 20 attempts.");
    }

    private static string GenerateCardNumber() =>
        "ID" + Random.Shared.NextInt64(1_000_000_000, 10_000_000_000).ToString(CultureInfo.InvariantCulture);
}
