using System.Globalization;
using Bogus;
using Microsoft.EntityFrameworkCore;
using Nrs.Application.Search;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;
using Nrs.Infrastructure.Persistence;

// Disambiguate from Bogus.Person — we always mean the domain entity here.
using Person = Nrs.Domain.Entities.Person;

namespace Nrs.Infrastructure.Seed;

/// <summary>
/// Generates realistic, bilingual sample data for the POC: 100 persons, each with
/// at least one ID card and one passport, plus a 1:1 address and contact record.
/// Generation is deterministic (fixed seed) so runs and tests are reproducible.
/// <para>
/// Coherence is enforced: a person's name pool, nationality, place of birth,
/// occupation and mother's name always belong to the same culture — an Omani-named
/// person is always <c>OMN</c>, and an expat always carries a matching non-OMN code.
/// Marital status respects age and student status; ID-card type matches nationality;
/// and document issue dates never precede the person's date of birth.
/// </para>
/// </summary>
public static class DataSeeder
{
    private const int DefaultCount = 100;
    private const int Seed = 20260628;

    // Roughly 85% of the population is Omani; the rest are expats. The threshold is
    // applied against a per-person dice roll so the split is deterministic.
    private const double OmaniShare = 0.85;

    // The "as of" date used for age-derived logic (occupation, marital status).
    private static readonly DateOnly AsOfDate = new(2026, 6, 28);

    // --- Bilingual name/value pairs (Arabic, English) used throughout. ---
    private readonly record struct Bilingual(string Ar, string En);

    private readonly record struct Wilayat(string Ar, string En);

    private readonly record struct Governorate(string Ar, string En, Wilayat[] Wilayats);

    /// <summary>An expat name pool keyed to a single nationality code.</summary>
    private readonly record struct ExpatPool(string Code, Bilingual[] Given, Bilingual[] Family);

    /// <summary>A foreign town of birth keyed to its nationality code.</summary>
    private readonly record struct ForeignTown(string Code, string Ar, string En);

    // ----------------------------------------------------------------------
    // OMANI REFERENCE DATA
    // ----------------------------------------------------------------------

    private static readonly Bilingual[] OmaniMaleNames =
    [
        new("محمد", "Mohammed"), new("أحمد", "Ahmed"), new("علي", "Ali"), new("سعيد", "Said"),
        new("سالم", "Salim"), new("خلفان", "Khalfan"), new("حمد", "Hamad"), new("حمدان", "Hamdan"),
        new("سلطان", "Sultan"), new("ناصر", "Nasser"), new("خالد", "Khalid"), new("عبدالله", "Abdullah"),
        new("يوسف", "Yousuf"), new("سيف", "Saif"), new("هلال", "Hilal"), new("راشد", "Rashid"),
        new("سليمان", "Sulaiman"), new("طالب", "Talib"), new("بدر", "Badr"), new("ماجد", "Majid"),
        new("مرهون", "Marhoon"), new("جمعة", "Juma"), new("عيسى", "Issa"), new("يعقوب", "Yaqoub"),
        new("محمود", "Mahmood"), new("قيس", "Qais"), new("زايد", "Zayid"), new("حميد", "Humaid"),
        new("طلال", "Talal"), new("إبراهيم", "Ibrahim"),
    ];

    private static readonly Bilingual[] OmaniFemaleNames =
    [
        new("فاطمة", "Fatma"), new("عائشة", "Aisha"), new("مريم", "Maryam"), new("خديجة", "Khadija"),
        new("سلمى", "Salma"), new("زينب", "Zainab"), new("آمنة", "Amna"), new("حوراء", "Hawra"),
        new("بثينة", "Buthaina"), new("أسماء", "Asma"), new("شمسة", "Shamsa"), new("مها", "Maha"),
        new("منى", "Mona"), new("رحمة", "Rahma"), new("سمية", "Sumaya"), new("نوال", "Nawal"),
        new("حنان", "Hanan"), new("نجوى", "Najwa"), new("لطيفة", "Latifa"), new("بدرية", "Badriya"),
        new("عالية", "Aliya"), new("شريفة", "Sharifa"), new("منيرة", "Munira"), new("وفاء", "Wafa"),
        new("هدى", "Huda"), new("سعادة", "Saada"), new("ثريا", "Thuraya"), new("منال", "Manal"),
        new("ريم", "Reem"), new("نور", "Noor"),
    ];

    private static readonly Bilingual[] OmaniFamilyNames =
    [
        new("الحارثي", "Al-Harthy"), new("البلوشي", "Al-Balushi"), new("الهنائي", "Al-Hinai"),
        new("السعدي", "Al-Saadi"), new("الريامي", "Al-Riyami"), new("اللواتي", "Al-Lawati"),
        new("الحبسي", "Al-Habsi"), new("الهاشمي", "Al-Hashmi"), new("البوسعيدي", "Al-Busaidi"),
        new("المعمري", "Al-Maamari"), new("الكندي", "Al-Kindi"), new("الوهيبي", "Al-Wahaibi"),
        new("الغافري", "Al-Ghafri"), new("الرواحي", "Al-Rawahi"), new("المحروقي", "Al-Mahrouqi"),
        new("النبهاني", "Al-Nabhani"), new("السيابي", "Al-Siyabi"), new("العامري", "Al-Amri"),
        new("الفارسي", "Al-Farsi"), new("الزدجالي", "Al-Zadjali"), new("المخيني", "Al-Mukhaini"),
        new("البطاشي", "Al-Battashi"), new("الشكيلي", "Al-Shukaili"), new("العبري", "Al-Abri"),
        new("الهاجري", "Al-Hajri"), new("الصبحي", "Al-Subhi"), new("الراشدي", "Al-Rashdi"),
        new("الخنجري", "Al-Khanjari"), new("اليحيائي", "Al-Yahyai"), new("المسكري", "Al-Maskari"),
    ];

    private static readonly Bilingual[] OmaniTowns =
    [
        new("مسقط", "Muscat"), new("صلالة", "Salalah"), new("صحار", "Sohar"), new("نزوى", "Nizwa"),
        new("صور", "Sur"), new("عبري", "Ibri"), new("إبراء", "Ibra"), new("الرستاق", "Rustaq"),
        new("بهلاء", "Bahla"), new("بركاء", "Barka"), new("خصب", "Khasab"), new("البريمي", "Al Buraimi"),
        new("السيب", "Seeb"), new("بوشر", "Bawshar"), new("صحم", "Saham"), new("أدم", "Adam"),
    ];

    // Adult Omani occupations. Note: "University Student" is intentionally retained as a
    // student occupation; marital-status generation treats any student as SINGLE/MARRIED only.
    private static readonly Bilingual[] OmaniOccupations =
    [
        new("موظف حكومي", "Government Employee"), new("معلم", "Teacher"), new("طبيب", "Physician"),
        new("ممرض", "Nurse"), new("مهندس", "Engineer"), new("شرطي", "Police Officer"),
        new("جندي", "Soldier"), new("صياد", "Fisherman"), new("مزارع", "Farmer"),
        new("تاجر", "Merchant"), new("محاسب", "Accountant"), new("سائق", "Driver"),
        new("كهربائي", "Electrician"), new("إمام", "Imam"), new("طالب جامعي", "University Student"),
        new("عامل في قطاع النفط", "Oil Sector Worker"), new("موظف بنك", "Bank Clerk"),
        new("موظف مدني", "Civil Servant"),
    ];

    private static readonly Governorate[] Governorates =
    [
        new("مسقط", "Muscat",
        [
            new("مطرح", "Muttrah"), new("بوشر", "Bawshar"), new("السيب", "Seeb"),
            new("العامرات", "Al Amerat"), new("قريات", "Qurayyat"), new("مسقط", "Muscat"),
        ]),
        new("ظفار", "Dhofar",
        [
            new("صلالة", "Salalah"), new("طاقة", "Taqah"), new("مرباط", "Mirbat"),
            new("ثمريت", "Thumrait"), new("رخيوت", "Rakhyut"), new("ضلكوت", "Dhalkut"),
            new("سدح", "Sadah"), new("شليم وجزر الحلانيات", "Shalim and the Hallaniyat Islands"),
            new("مقشن", "Muqshin"), new("المزيونة", "Al Mazyona"),
        ]),
        new("الداخلية", "Ad Dakhiliyah",
        [
            new("نزوى", "Nizwa"), new("بهلاء", "Bahla"), new("منح", "Manah"),
            new("الحمراء", "Al Hamra"), new("أدم", "Adam"), new("إزكي", "Izki"),
            new("سمائل", "Samail"), new("بدبد", "Bidbid"),
        ]),
        new("شمال الباطنة", "Al Batinah North",
        [
            new("صحار", "Sohar"), new("شناص", "Shinas"), new("لوى", "Liwa"),
            new("صحم", "Saham"), new("الخابورة", "Al Khaburah"), new("السويق", "As Suwayq"),
        ]),
        new("جنوب الباطنة", "Al Batinah South",
        [
            new("الرستاق", "Ar Rustaq"), new("العوابي", "Al Awabi"), new("نخل", "Nakhal"),
            new("وادي المعاول", "Wadi Al Maawil"), new("بركاء", "Barka"), new("المصنعة", "Al Musannah"),
        ]),
        new("شمال الشرقية", "Ash Sharqiyah North",
        [
            new("إبراء", "Ibra"), new("المضيبي", "Al Mudaybi"), new("بدية", "Bidiyah"),
            new("القابل", "Al Qabil"), new("وادي بني خالد", "Wadi Bani Khalid"),
            new("دماء والطائيين", "Dima Wa At Taiyyin"),
        ]),
        new("جنوب الشرقية", "Ash Sharqiyah South",
        [
            new("صور", "Sur"), new("الكامل والوافي", "Al Kamil Wal Wafi"),
            new("جعلان بني بو حسن", "Jaalan Bani Bu Hassan"), new("جعلان بني بو علي", "Jaalan Bani Bu Ali"),
            new("مصيرة", "Masirah"),
        ]),
        new("الظاهرة", "Adh Dhahirah",
        [
            new("عبري", "Ibri"), new("ينقل", "Yanqul"), new("ضنك", "Dhank"),
        ]),
        new("البريمي", "Al Buraimi",
        [
            new("البريمي", "Al Buraimi"), new("محضة", "Mahdah"), new("السنينة", "As Sunaynah"),
        ]),
        new("الوسطى", "Al Wusta",
        [
            new("هيما", "Haima"), new("محوت", "Mahut"), new("الدقم", "Ad Duqm"), new("الجازر", "Al Jazir"),
        ]),
        new("مسندم", "Musandam",
        [
            new("خصب", "Khasab"), new("بخا", "Bukha"), new("دبا البيعة", "Daba Al Bayah"),
            new("مدحاء", "Madha"),
        ]),
    ];

    // Fast, fail-loud lookup of governorates by English name (used for biased expat placement).
    private static readonly Dictionary<string, Governorate> GovernoratesByName =
        Governorates.ToDictionary(g => g.En, StringComparer.Ordinal);

    // ----------------------------------------------------------------------
    // EXPAT REFERENCE DATA
    // ----------------------------------------------------------------------
    // Every Code below exists in the seeded NATIONALITY reference data (HasData), so the
    // NationalityCode FK is always satisfiable. None of these are GCC member states, so
    // expat ID cards are never issued as CardType.GCC (see BuildIdCards).

    private static readonly ExpatPool[] ExpatPools =
    [
        new("IND",
            [
                new("راجيش", "Rajesh"), new("سوريش", "Suresh"), new("أنيل", "Anil"), new("فيجاي", "Vijay"),
                new("بريا", "Priya"), new("ديباك", "Deepak"), new("راميش", "Ramesh"), new("سونيتا", "Sunita"),
            ],
            [
                new("كومار", "Kumar"), new("شارما", "Sharma"), new("ناير", "Nair"), new("مينون", "Menon"),
                new("باتيل", "Patel"), new("ريدي", "Reddy"), new("بيلاي", "Pillai"), new("سينغ", "Singh"),
            ]),
        new("PAK",
            [
                new("محمد", "Muhammad"), new("عمران", "Imran"), new("بلال", "Bilal"), new("آصف", "Asif"),
                new("فرحان", "Farhan"), new("زيشان", "Zeeshan"), new("عائشة", "Ayesha"), new("سائمة", "Saima"),
            ],
            [
                new("خان", "Khan"), new("مالك", "Malik"), new("بهٹي", "Bhatti"), new("تشودري", "Chaudhry"),
                new("حسين", "Hussain"), new("إقبال", "Iqbal"), new("شيخ", "Sheikh"), new("رضا", "Raza"),
            ]),
        new("BGD",
            [
                new("رحيم", "Rahim"), new("كريم", "Karim"), new("شكيل", "Shakil"), new("جمال", "Jamal"),
                new("ميزان الرحمن", "Mizanur"), new("حبيب", "Habib"), new("نسرين", "Nasrin"), new("سلمى", "Salma"),
            ],
            [
                new("إسلام", "Islam"), new("الرحمن", "Rahman"), new("أحمد", "Ahmed"), new("حسين", "Hossain"),
                new("الدين", "Uddin"), new("ميا", "Mia"), new("ساركر", "Sarker"), new("تشودري", "Chowdhury"),
            ]),
        new("EGY",
            [
                new("أحمد", "Ahmed"), new("محمد", "Mohamed"), new("محمود", "Mahmoud"), new("مصطفى", "Mostafa"),
                new("حسام", "Hossam"), new("أميرة", "Amira"), new("منى", "Mona"), new("ياسمين", "Yasmin"),
            ],
            [
                new("حسن", "Hassan"), new("علي", "Ali"), new("إبراهيم", "Ibrahim"), new("سعيد", "Said"),
                new("عبد الرحمن", "Abdelrahman"), new("السيد", "El-Sayed"), new("منصور", "Mansour"), new("فوزي", "Fawzy"),
            ]),
        new("PHL",
            [
                new("خوسيه", "Jose"), new("مارك", "Mark"), new("جون", "John"), new("ماريا", "Maria"),
                new("جيني", "Jenny"), new("روميل", "Rommel"), new("غرايس", "Grace"), new("كريستينا", "Cristina"),
            ],
            [
                new("سانتوس", "Santos"), new("رييس", "Reyes"), new("كروز", "Cruz"), new("غارسيا", "Garcia"),
                new("دي لا كروز", "Dela Cruz"), new("مندوزا", "Mendoza"), new("أكينو", "Aquino"), new("باوتيستا", "Bautista"),
            ]),
        new("LKA",
            [
                new("نيمال", "Nimal"), new("سونيل", "Sunil"), new("كاسون", "Kasun"), new("براديب", "Pradeep"),
                new("تشامارا", "Chamara"), new("ديلاني", "Dilani"), new("نيروشا", "Nirosha"), new("ساندوني", "Sanduni"),
            ],
            [
                new("بيريرا", "Perera"), new("فرناندو", "Fernando"), new("سيلفا", "Silva"), new("جايواردينا", "Jayawardena"),
                new("باندارا", "Bandara"), new("ويكراماسينغه", "Wickramasinghe"), new("غوناواردينا", "Gunawardena"), new("راجاباكسا", "Rajapaksa"),
            ]),
        new("GBR",
            [
                new("جيمس", "James"), new("ديفيد", "David"), new("مايكل", "Michael"), new("سارة", "Sarah"),
                new("إيما", "Emma"), new("دانيال", "Daniel"), new("صوفي", "Sophie"), new("أندرو", "Andrew"),
            ],
            [
                new("سميث", "Smith"), new("جونز", "Jones"), new("براون", "Brown"), new("تايلور", "Taylor"),
                new("ويلسون", "Wilson"), new("طومسون", "Thompson"), new("كلارك", "Clarke"), new("روبرتس", "Roberts"),
            ]),
        new("USA",
            [
                new("روبرت", "Robert"), new("ويليام", "William"), new("كريستوفر", "Christopher"), new("جينيفر", "Jennifer"),
                new("جيسيكا", "Jessica"), new("ماثيو", "Matthew"), new("آشلي", "Ashley"), new("براين", "Brian"),
            ],
            [
                new("جونسون", "Johnson"), new("ويليامز", "Williams"), new("ميلر", "Miller"), new("ديفيس", "Davis"),
                new("أندرسون", "Anderson"), new("مارتينيز", "Martinez"), new("طومسون", "Thompson"), new("هاريس", "Harris"),
            ]),
        new("JOR",
            [
                new("عمر", "Omar"), new("خالد", "Khaled"), new("طارق", "Tariq"), new("رامي", "Rami"),
                new("لارا", "Lara"), new("دينا", "Dina"), new("هالة", "Hala"), new("بشار", "Bashar"),
            ],
            [
                new("الخطيب", "Al-Khatib"), new("حداد", "Haddad"), new("النمري", "Nimri"), new("المصري", "Al-Masri"),
                new("عبيدات", "Obeidat"), new("الزعبي", "Zoubi"), new("الطراونة", "Tarawneh"), new("أبو رحمة", "Abu Rahmeh"),
            ]),
        new("YEM",
            [
                new("صالح", "Saleh"), new("عبد الله", "Abdullah"), new("فيصل", "Faisal"), new("نبيل", "Nabil"),
                new("هاني", "Hani"), new("أمل", "Amal"), new("سمية", "Sumaya"), new("وضاح", "Wadah"),
            ],
            [
                new("الحضرمي", "Al-Hadrami"), new("باعمر", "Ba Omar"), new("الصنعاني", "Al-Sanani"), new("الشيباني", "Al-Shaibani"),
                new("المقطري", "Al-Maqtari"), new("باوزير", "Ba Wazir"), new("العدني", "Al-Adani"), new("المرواني", "Al-Marwani"),
            ]),
    ];

    private static readonly Bilingual[] ExpatOccupations =
    [
        new("عامل بناء", "Construction Worker"), new("عاملة منزلية", "Domestic Worker"),
        new("محاسب", "Accountant"), new("ممرض", "Nurse"), new("مهندس", "Engineer"),
        new("سائق", "Driver"), new("مندوب مبيعات", "Sales Associate"), new("كهربائي", "Electrician"),
        new("سباك", "Plumber"), new("نجار", "Carpenter"), new("لحام", "Welder"), new("طباخ", "Cook"),
        new("نادل", "Waiter"), new("حارس أمن", "Security Guard"), new("عامل نظافة", "Cleaner"),
        new("ميكانيكي", "Mechanic"), new("مدرس", "Teacher"), new("طبيب", "Doctor"),
        new("فني تقنية معلومات", "IT Technician"), new("خياط", "Tailor"), new("حلاق", "Barber"),
        new("أمين مخزن", "Store Keeper"),
    ];

    private static readonly ForeignTown[] ForeignTowns =
    [
        new("IND", "كوتشي", "Kochi"), new("IND", "مومباي", "Mumbai"),
        new("IND", "ثيروفانانثابورام", "Thiruvananthapuram"), new("IND", "حيدر آباد", "Hyderabad"),
        new("PAK", "كراتشي", "Karachi"), new("PAK", "لاهور", "Lahore"),
        new("PAK", "بيشاور", "Peshawar"), new("PAK", "روالبندي", "Rawalpindi"),
        new("BGD", "دكا", "Dhaka"), new("BGD", "شيتاغونغ", "Chittagong"),
        new("BGD", "سيلهيت", "Sylhet"), new("BGD", "كوميلا", "Comilla"),
        new("EGY", "القاهرة", "Cairo"), new("EGY", "الإسكندرية", "Alexandria"),
        new("EGY", "طنطا", "Tanta"), new("EGY", "أسيوط", "Assiut"),
        new("PHL", "مانيلا", "Manila"), new("PHL", "سيبو", "Cebu"),
        new("PHL", "دافاو", "Davao"), new("PHL", "كيزون سيتي", "Quezon City"),
        new("LKA", "كولومبو", "Colombo"), new("LKA", "كاندي", "Kandy"),
        new("LKA", "غالي", "Galle"), new("LKA", "نيغومبو", "Negombo"),
        new("GBR", "لندن", "London"), new("GBR", "مانشستر", "Manchester"),
        new("GBR", "برمنغهام", "Birmingham"), new("GBR", "أبردين", "Aberdeen"),
        new("USA", "هيوستن", "Houston"), new("USA", "نيويورك", "New York"),
        new("USA", "شيكاغو", "Chicago"), new("USA", "لوس أنجلوس", "Los Angeles"),
        new("JOR", "عمّان", "Amman"), new("JOR", "إربد", "Irbid"),
        new("JOR", "الزرقاء", "Zarqa"), new("JOR", "العقبة", "Aqaba"),
        new("YEM", "صنعاء", "Sana'a"), new("YEM", "عدن", "Aden"),
        new("YEM", "تعز", "Taiz"), new("YEM", "المكلا", "Mukalla"),
    ];

    // ABO/Rh blood-group distribution, tuned toward the Gulf / South-Asian populations
    // that dominate this dataset (O+ and B+ common; Rh-negative total ~6.5%). Weights sum to 1.0.
    private static readonly string[] BloodTypes =
        ["O+", "B+", "A+", "AB+", "O-", "A-", "B-", "AB-"];

    private static readonly float[] BloodTypeWeights =
        [0.34f, 0.24f, 0.22f, 0.065f, 0.04f, 0.02f, 0.01f, 0.005f];

    // Governorates that expats most commonly live in (Muscat / Al Batinah biased).
    private static readonly string[] ExpatHomeGovernorates =
        ["Muscat", "Muscat", "Muscat", "Al Batinah North", "Al Batinah South", "Dhofar"];

    /// <summary>
    /// Seeds the database if it is empty (idempotent), then backfills the normalized
    /// <see cref="Person.NameSearch"/> column for any rows missing it — so both fresh seeds
    /// and databases seeded before the column existed end up searchable.
    /// </summary>
    public static async Task SeedAsync(NrsDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.Persons.AnyAsync(cancellationToken))
        {
            db.Persons.AddRange(GeneratePersons(DefaultCount));
            await db.SaveChangesAsync(cancellationToken);
        }

        await BackfillNameSearchAsync(db, cancellationToken);
    }

    /// <summary>
    /// Populates <see cref="Person.NameSearch"/> for any rows where it is missing. Cheap and
    /// idempotent: once every row is populated the lookup returns nothing and this is a no-op.
    /// </summary>
    private static async Task BackfillNameSearchAsync(NrsDbContext db, CancellationToken cancellationToken)
    {
        var stale = await db.Persons
            .Where(p => p.NameSearch == null || p.NameSearch == "")
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return;
        }

        foreach (var p in stale)
        {
            p.NameSearch = BuildNameSearch(p.FirstNameAr, p.FamilyNameAr, p.FirstNameEn, p.FamilyNameEn);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Normalized, space-joined concatenation of the four name parts (for search).</summary>
    public static string BuildNameSearch(string firstAr, string familyAr, string firstEn, string familyEn)
    {
        var parts = new[]
        {
            NameNormalizer.Normalize(firstAr),
            NameNormalizer.Normalize(familyAr),
            NameNormalizer.Normalize(firstEn),
            NameNormalizer.Normalize(familyEn),
        };
        return string.Join(' ', parts.Where(s => s.Length > 0));
    }

    /// <summary>
    /// Builds a deterministic set of <paramref name="count"/> persons with documents,
    /// address and contact. Exposed so tests can generate data without a database.
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
            var isOmani = faker.Random.Double() < OmaniShare;

            string crn;
            do
            {
                crn = faker.Random.ReplaceNumbers("########"); // 8 digits (fits CIVIL_NUMBER(9))
            }
            while (!usedCrns.Add(crn));

            var dob = DateOnly.FromDateTime(faker.Date.Between(new DateTime(1960, 1, 1), new DateTime(2015, 12, 31)));
            var age = AgeOn(dob, AsOfDate);
            var isChild = age < 18;

            Person person = isOmani
                ? BuildOmani(faker, crn, isMale, dob, age, isChild)
                : BuildExpat(faker, crn, isMale, dob, age, isChild);

            people.Add(person);
        }

        return people;
    }

    private static Person BuildOmani(Faker faker, string crn, bool isMale, DateOnly dob, int age, bool isChild)
    {
        var given = faker.PickRandom(isMale ? OmaniMaleNames : OmaniFemaleNames);
        var family = faker.PickRandom(OmaniFamilyNames);

        // Mother: Omani female given name + an Omani family name.
        var motherGiven = faker.PickRandom(OmaniFemaleNames);
        var motherFamily = faker.PickRandom(OmaniFamilyNames);

        var birthTown = faker.PickRandom(OmaniTowns);
        var occupation = PickOccupation(faker, OmaniOccupations, isChild);
        var (gov, wil) = PickOmaniAddressFromAny(faker);

        return BuildPerson(
            faker, crn, "OMN", isMale, dob, age, isChild,
            given, family, motherGiven, motherFamily,
            birthTown, occupation, gov, wil);
    }

    private static Person BuildExpat(Faker faker, string crn, bool isMale, DateOnly dob, int age, bool isChild)
    {
        var pool = faker.PickRandom(ExpatPools);

        var given = faker.PickRandom(pool.Given);
        var family = faker.PickRandom(pool.Family);

        // Mother shares the same nationality pool (given + family from that culture).
        var motherGiven = faker.PickRandom(pool.Given);
        var motherFamily = faker.PickRandom(pool.Family);

        // Town of birth must match the expat's nationality code.
        var birthTown = PickForeignTown(faker, pool.Code);
        var occupation = PickOccupation(faker, ExpatOccupations, isChild);

        // Expats live in Oman — bias their address toward Muscat / Al Batinah.
        var (gov, wil) = PickExpatAddress(faker);

        return BuildPerson(
            faker, crn, pool.Code, isMale, dob, age, isChild,
            given, family, motherGiven, motherFamily,
            new Bilingual(birthTown.Ar, birthTown.En), occupation, gov, wil);
    }

    private static Person BuildPerson(
        Faker faker,
        string crn,
        string nationality,
        bool isMale,
        DateOnly dob,
        int age,
        bool isChild,
        Bilingual given,
        Bilingual family,
        Bilingual motherGiven,
        Bilingual motherFamily,
        Bilingual birthTown,
        Bilingual occupation,
        Governorate gov,
        Wilayat wil)
    {
        // Students (children and university students) are never widowed/divorced.
        var isStudent = IsStudentOccupation(occupation);

        return new Person
        {
            CivilNumber = crn,
            FirstNameEn = given.En,
            FamilyNameEn = family.En,
            FirstNameAr = given.Ar,
            FamilyNameAr = family.Ar,
            NameSearch = BuildNameSearch(given.Ar, family.Ar, given.En, family.En),
            DateOfBirth = dob,
            Gender = isMale ? "M" : "F",
            NationalityCode = nationality,
            Status = faker.Random.WeightedRandom(
                [PersonStatus.ACTIVE, PersonStatus.DECEASED, PersonStatus.MERGED],
                [0.9f, 0.06f, 0.04f]),
            PhotoPath = $"/photos/{crn}.jpg",

            // Extended biographic data — all populated for every person.
            PlaceOfBirthEn = birthTown.En,
            PlaceOfBirthAr = birthTown.Ar,
            MotherNameEn = $"{motherGiven.En} {motherFamily.En}",
            MotherNameAr = $"{motherGiven.Ar} {motherFamily.Ar}",
            MaritalStatus = PickMaritalStatus(faker, age, isStudent),
            BloodType = faker.Random.WeightedRandom(BloodTypes, BloodTypeWeights),
            OccupationEn = occupation.En,
            OccupationAr = occupation.Ar,

            Address = BuildAddress(faker, crn, gov, wil),
            Contact = BuildContact(faker, crn, given.En, family.En),
            IdCards = BuildIdCards(faker, crn, nationality, occupation, dob),
            Passports = BuildPassports(faker, crn, dob),
        };
    }

    private static Address BuildAddress(Faker faker, string crn, Governorate gov, Wilayat wil)
    {
        return new Address
        {
            CivilNumber = crn,
            Governorate = gov.En,
            Wilayat = wil.En,
            Village = faker.Random.Bool(0.5f) ? null : $"Village {faker.Random.Int(1, 40)}",
            Street = $"Street {faker.Random.Int(1, 120)}",
            BuildingNumber = faker.Random.Int(1, 999).ToString(CultureInfo.InvariantCulture),
            PostalCode = faker.Random.Int(100, 999).ToString(CultureInfo.InvariantCulture),
        };
    }

    private static Contact BuildContact(Faker faker, string crn, string firstNameEn, string familyNameEn)
    {
        // Oman mobile numbers: +968 then 9XXXXXXX.
        var mobile = "+9689" + faker.Random.ReplaceNumbers("#######");

        var localPart = $"{firstNameEn}.{familyNameEn}"
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        // Use a reserved example domain (RFC 2606) so addresses can never collide with
        // real domains — '.om' would be Oman's live ccTLD and is not safe for synthetic data.
        var email = $"{localPart}{faker.Random.Int(1, 999)}@example.com";

        return new Contact
        {
            CivilNumber = crn,
            Mobile = mobile,
            Email = email,
        };
    }

    private static List<IdCard> BuildIdCards(
        Faker faker, string crn, string nationality, Bilingual occupation, DateOnly dob)
    {
        // Card type must be coherent with nationality:
        //  - OMN  → OMANI
        //  - non-GCC expats (all pools here) → RESIDENT, or INVESTOR only when the
        //    occupation implies running a business (Merchant). None of the expat
        //    nationalities are GCC states, so CardType.GCC is never issued.
        CardType cardType;
        if (nationality == "OMN")
        {
            cardType = CardType.OMANI;
        }
        else if (IsBusinessOccupation(occupation) && faker.Random.Bool(0.5f))
        {
            cardType = CardType.INVESTOR;
        }
        else
        {
            cardType = CardType.RESIDENT;
        }

        var cards = new List<IdCard>();

        // Earliest plausible issue date: never before birth, and not before the seeding window.
        var earliest = MaxDate(new DateOnly(2015, 1, 1), dob);
        var latest = new DateOnly(2023, 12, 31);
        if (earliest > latest)
        {
            earliest = latest; // very young child: clamp to the window's end.
        }

        foreach (var _ in Enumerable.Range(0, faker.Random.Int(1, 2)))
        {
            var issue = RandomDateOnly(faker, earliest, latest);
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

    private static List<Passport> BuildPassports(Faker faker, string crn, DateOnly dob)
    {
        var passports = new List<Passport>();

        // Earliest plausible issue date: never before birth, and not before the seeding window.
        var earliest = MaxDate(new DateOnly(2016, 1, 1), dob);
        var latest = new DateOnly(2023, 12, 31);
        if (earliest > latest)
        {
            earliest = latest;
        }

        foreach (var _ in Enumerable.Range(0, faker.Random.Int(1, 2)))
        {
            var issue = RandomDateOnly(faker, earliest, latest);
            passports.Add(new Passport
            {
                CivilNumber = crn,
                PassportNumber = faker.Random.Replace("?#######").ToUpperInvariant(),

                // Non-ordinary passports are rare in a general population (~3% total),
                // including a very small share of ROYAL_DIPLOMATIC for full enum coverage.
                PassportType = faker.Random.WeightedRandom(
                    [PassportType.ORDINARY, PassportType.DIPLOMATIC, PassportType.SERVICE, PassportType.SPECIAL, PassportType.ROYAL_DIPLOMATIC],
                    [0.97f, 0.012f, 0.01f, 0.005f, 0.003f]),
                IssueDate = issue,
                ExpiryDate = issue.AddYears(10),
                Status = faker.Random.WeightedRandom(
                    [PassportStatus.ACTIVE, PassportStatus.EXPIRED, PassportStatus.CANCELLED, PassportStatus.LOST, PassportStatus.STOLEN],
                    [0.78f, 0.12f, 0.04f, 0.03f, 0.03f]),
            });
        }

        return passports;
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static int AgeOn(DateOnly dob, DateOnly asOf)
    {
        var age = asOf.Year - dob.Year;
        if (dob > asOf.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    private static DateOnly MaxDate(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly RandomDateOnly(Faker faker, DateOnly earliest, DateOnly latest)
    {
        if (earliest >= latest)
        {
            return earliest;
        }

        var start = earliest.ToDateTime(TimeOnly.MinValue);
        var end = latest.ToDateTime(TimeOnly.MinValue);
        return DateOnly.FromDateTime(faker.Date.Between(start, end));
    }

    /// <summary>Occupation respects age: children are always students.</summary>
    private static Bilingual PickOccupation(Faker faker, Bilingual[] pool, bool isChild)
    {
        return isChild ? new Bilingual("طالب", "Student") : faker.PickRandom(pool);
    }

    /// <summary>True when the occupation represents a student (so the person can't be widowed/divorced).</summary>
    private static bool IsStudentOccupation(Bilingual occupation) =>
        occupation.En == "Student" || occupation.En == "University Student";

    /// <summary>True when the occupation implies running a business (eligible for an INVESTOR card).</summary>
    private static bool IsBusinessOccupation(Bilingual occupation) =>
        occupation.En == "Merchant";

    /// <summary>
    /// Marital status by age: minors are always single; young adults mostly single;
    /// older adults mostly married with a small share of divorced/widowed. Students are
    /// constrained to SINGLE/MARRIED regardless of age (never widowed/divorced).
    /// </summary>
    private static MaritalStatus PickMaritalStatus(Faker faker, int age, bool isStudent)
    {
        if (age < 18)
        {
            return MaritalStatus.SINGLE;
        }

        if (isStudent)
        {
            return faker.Random.WeightedRandom(
                [MaritalStatus.SINGLE, MaritalStatus.MARRIED],
                [0.85f, 0.15f]);
        }

        if (age < 25)
        {
            return faker.Random.WeightedRandom(
                [MaritalStatus.SINGLE, MaritalStatus.MARRIED],
                [0.8f, 0.2f]);
        }

        if (age < 40)
        {
            return faker.Random.WeightedRandom(
                [MaritalStatus.MARRIED, MaritalStatus.SINGLE, MaritalStatus.DIVORCED],
                [0.7f, 0.22f, 0.08f]);
        }

        return faker.Random.WeightedRandom(
            [MaritalStatus.MARRIED, MaritalStatus.WIDOWED, MaritalStatus.DIVORCED, MaritalStatus.SINGLE],
            [0.72f, 0.13f, 0.1f, 0.05f]);
    }

    /// <summary>Picks any valid governorate + wilayat pair (Omani residents).</summary>
    private static (Governorate Gov, Wilayat Wil) PickOmaniAddressFromAny(Faker faker)
    {
        var gov = faker.PickRandom(Governorates);
        var wil = faker.PickRandom(gov.Wilayats);
        return (gov, wil);
    }

    /// <summary>Picks a governorate biased toward Muscat / Al Batinah, then a valid wilayat.</summary>
    private static (Governorate Gov, Wilayat Wil) PickExpatAddress(Faker faker)
    {
        var govName = faker.PickRandom(ExpatHomeGovernorates);

        // Fail loudly on a bad name rather than NRE-ing inside PickRandom on a default struct.
        if (!GovernoratesByName.TryGetValue(govName, out var gov))
        {
            throw new InvalidOperationException(
                $"ExpatHomeGovernorates references '{govName}', which is not a known governorate name.");
        }

        var wil = faker.PickRandom(gov.Wilayats);
        return (gov, wil);
    }

    /// <summary>Picks a foreign town of birth that matches the given nationality code.</summary>
    private static ForeignTown PickForeignTown(Faker faker, string code)
    {
        var matches = Array.FindAll(ForeignTowns, t => t.Code == code);
        if (matches.Length == 0)
        {
            throw new InvalidOperationException($"No foreign towns are defined for nationality code '{code}'.");
        }

        return faker.PickRandom(matches);
    }
}