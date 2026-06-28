using Bogus;
using Microsoft.EntityFrameworkCore;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

// Disambiguate from Bogus.Person — we always mean the domain entity here.
using Person = Nrs.Domain.Entities.Person;

namespace Nrs.Infrastructure.Seed;

/// <summary>
/// Generates realistic, bilingual sample data for the POC: 100+ persons, each with
/// at least one ID card and one passport. Generation is deterministic (fixed seed) so
/// runs and tests are reproducible.
/// </summary>
public static class DataSeeder
{
    private const int DefaultCount = 100;
    private const int Seed = 20260628;

    // Paired Arabic/English names so each record reads as one consistent person.
    private static readonly (string Ar, string En)[] MaleFirstNames =
    [
        ("محمد", "Mohammed"), ("أحمد", "Ahmed"), ("علي", "Ali"), ("سالم", "Salim"),
        ("سعيد", "Said"), ("خالد", "Khalid"), ("حمد", "Hamad"), ("سلطان", "Sultan"),
        ("ناصر", "Nasser"), ("يوسف", "Yousuf"), ("عبدالله", "Abdullah"), ("ماجد", "Majid"),
        ("طلال", "Talal"), ("فيصل", "Faisal"), ("راشد", "Rashid"),
    ];

    private static readonly (string Ar, string En)[] FemaleFirstNames =
    [
        ("فاطمة", "Fatma"), ("عائشة", "Aisha"), ("مريم", "Maryam"), ("آمنة", "Amna"),
        ("سلمى", "Salma"), ("نور", "Noor"), ("هدى", "Huda"), ("ليلى", "Layla"),
        ("بثينة", "Buthaina"), ("منى", "Muna"), ("زينب", "Zainab"), ("شيخة", "Shaikha"),
    ];

    private static readonly (string Ar, string En)[] FamilyNames =
    [
        ("البلوشي", "Al Balushi"), ("الحبسي", "Al Habsi"), ("الريامي", "Al Riyami"),
        ("السعيدي", "Al Saidi"), ("الحارثي", "Al Harthy"), ("الزدجالي", "Al Zadjali"),
        ("المعمري", "Al Maamari"), ("البوسعيدي", "Al Busaidi"), ("الكندي", "Al Kindi"),
        ("العامري", "Al Amri"), ("الرواحي", "Al Rawahi"), ("الهنائي", "Al Hinai"),
        ("الوهيبي", "Al Wahaibi"), ("المحروقي", "Al Mahrouqi"), ("الشكيلي", "Al Shukaili"),
    ];

    private static readonly string[] Nationalities = ["OMN", "OMN", "OMN", "OMN", "IND", "PAK", "EGY", "BGD", "PHL", "GBR"];

    /// <summary>
    /// Seeds the database if it is empty. Idempotent: a no-op when persons already exist.
    /// </summary>
    public static async Task SeedAsync(NrsDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.Persons.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Persons.AddRange(GeneratePersons(DefaultCount));
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a deterministic set of <paramref name="count"/> persons with documents.
    /// Exposed so tests can generate data without a database.
    /// </summary>
    public static IReadOnlyList<Person> GeneratePersons(int count = DefaultCount)
    {
        Randomizer.Seed = new Random(Seed);
        var faker = new Faker();

        var usedCrns = new HashSet<string>();
        var people = new List<Person>(count);

        for (var i = 0; i < count; i++)
        {
            var isMale = faker.Random.Bool();
            var first = faker.PickRandom(isMale ? MaleFirstNames : FemaleFirstNames);
            var family = faker.PickRandom(FamilyNames);

            string crn;
            do
            {
                crn = faker.Random.ReplaceNumbers("########"); // 8 digits
            }
            while (!usedCrns.Add(crn));

            var nationality = faker.PickRandom(Nationalities);
            var dob = DateOnly.FromDateTime(faker.Date.Between(new DateTime(1960, 1, 1), new DateTime(2005, 12, 31)));

            var person = new Person
            {
                CivilNumber = crn,
                FirstNameEn = first.En,
                FamilyNameEn = family.En,
                FirstNameAr = first.Ar,
                FamilyNameAr = family.Ar,
                DateOfBirth = dob,
                Gender = isMale ? "M" : "F",
                NationalityCode = nationality,
                Status = faker.Random.WeightedRandom(
                    [PersonStatus.ACTIVE, PersonStatus.DECEASED, PersonStatus.MERGED],
                    [0.9f, 0.06f, 0.04f]),
                PhotoPath = $"/photos/{crn}.jpg",
                IdCards = BuildIdCards(faker, crn, nationality),
                Passports = BuildPassports(faker, crn),
            };

            people.Add(person);
        }

        return people;
    }

    private static List<IdCard> BuildIdCards(Faker faker, string crn, string nationality)
    {
        var cardType = nationality == "OMN" ? CardType.OMANI : faker.PickRandom(CardType.RESIDENT, CardType.GCC, CardType.INVESTOR);
        var cards = new List<IdCard>();

        foreach (var _ in Enumerable.Range(0, faker.Random.Int(1, 2)))
        {
            var issue = DateOnly.FromDateTime(faker.Date.Between(new DateTime(2015, 1, 1), new DateTime(2023, 12, 31)));
            cards.Add(new IdCard
            {
                CivilNumber = crn,
                CardNumber = "ID" + faker.Random.ReplaceNumbers("##########"),
                IssueDate = issue,
                ExpiryDate = issue.AddYears(10),
                CardType = cardType,
                Status = faker.Random.WeightedRandom(
                    [CardStatus.ACTIVE, CardStatus.EXPIRED, CardStatus.BLOCKED, CardStatus.LOST],
                    [0.75f, 0.15f, 0.05f, 0.05f]),
            });
        }

        return cards;
    }

    private static List<Passport> BuildPassports(Faker faker, string crn)
    {
        var passports = new List<Passport>();

        foreach (var _ in Enumerable.Range(0, faker.Random.Int(1, 2)))
        {
            var issue = DateOnly.FromDateTime(faker.Date.Between(new DateTime(2016, 1, 1), new DateTime(2023, 12, 31)));
            passports.Add(new Passport
            {
                CivilNumber = crn,
                PassportNumber = faker.Random.Replace("?#######").ToUpperInvariant(),
                PassportType = faker.Random.WeightedRandom(
                    [PassportType.ORDINARY, PassportType.DIPLOMATIC, PassportType.SERVICE, PassportType.SPECIAL],
                    [0.88f, 0.04f, 0.04f, 0.04f]),
                IssueDate = issue,
                ExpiryDate = issue.AddYears(10),
                Status = faker.Random.WeightedRandom(
                    [PassportStatus.ACTIVE, PassportStatus.EXPIRED, PassportStatus.CANCELLED, PassportStatus.LOST, PassportStatus.STOLEN],
                    [0.78f, 0.12f, 0.04f, 0.03f, 0.03f]),
            });
        }

        return passports;
    }
}
