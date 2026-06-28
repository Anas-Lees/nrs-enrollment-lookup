using Nrs.Application.Dtos;
using Nrs.Application.Services;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;

namespace Nrs.Application.Tests;

public class PersonLookupServiceTests
{
    [Fact]
    public async Task SearchAsync_MapsEntitiesToSummaries_AndReturnsTotalCount()
    {
        var first = BuildPerson("11111111", firstNameEn: "Ahmed", status: PersonStatus.ACTIVE);
        var second = BuildPerson("22222222", firstNameEn: "Fatima", status: PersonStatus.DECEASED);
        var repository = new FakePersonRepository
        {
            SearchResult = [first, second],
            TotalCount = 42,
        };
        var service = new PersonLookupService(repository);

        var result = await service.SearchAsync(new PersonSearchCriteria());

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(42, result.TotalCount);

        var firstSummary = result.Items[0];
        Assert.Equal(first.CivilNumber, firstSummary.CivilNumber);
        Assert.Equal(first.FirstNameEn, firstSummary.FirstNameEn);
        Assert.Equal(first.Status, firstSummary.Status);
    }

    [Fact]
    public async Task SearchAsync_ClampsPageSize_WhenAboveMax()
    {
        var repository = new FakePersonRepository();
        var service = new PersonLookupService(repository);

        var result = await service.SearchAsync(new PersonSearchCriteria { Page = 1, PageSize = 999 });

        Assert.Equal(20, result.PageSize);
        Assert.NotNull(repository.LastCriteria);
        Assert.Equal(20, repository.LastCriteria!.PageSize);
    }

    [Fact]
    public async Task SearchAsync_ClampsPage_WhenBelowOne()
    {
        var repository = new FakePersonRepository();
        var service = new PersonLookupService(repository);

        var result = await service.SearchAsync(new PersonSearchCriteria { Page = 0, PageSize = 20 });

        Assert.Equal(1, result.Page);
        Assert.NotNull(repository.LastCriteria);
        Assert.Equal(1, repository.LastCriteria!.Page);
    }

    [Fact]
    public async Task SearchAsync_KeepsValidPaging()
    {
        var repository = new FakePersonRepository();
        var service = new PersonLookupService(repository);

        var result = await service.SearchAsync(new PersonSearchCriteria { Page = 3, PageSize = 25 });

        Assert.Equal(3, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.NotNull(repository.LastCriteria);
        Assert.Equal(3, repository.LastCriteria!.Page);
        Assert.Equal(25, repository.LastCriteria.PageSize);
    }

    [Fact]
    public async Task GetByCrnAsync_ReturnsNull_WhenRepositoryReturnsNull()
    {
        var repository = new FakePersonRepository { GetByCrnResult = null };
        var service = new PersonLookupService(repository);

        var result = await service.GetByCrnAsync("00000000");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByCrnAsync_MapsProfileIncludingDocuments()
    {
        var person = BuildPerson("33333333", firstNameEn: "Salim", status: PersonStatus.ACTIVE);
        person.IdCards = [BuildIdCard(cardNumber: "CARD-1", status: CardStatus.ACTIVE, cardType: CardType.OMANI)];
        person.Passports = [BuildPassport(passportNumber: "PASS-1", type: PassportType.ORDINARY, status: PassportStatus.ACTIVE)];
        var repository = new FakePersonRepository { GetByCrnResult = person };
        var service = new PersonLookupService(repository);

        var result = await service.GetByCrnAsync("33333333");

        Assert.NotNull(result);
        Assert.Equal("33333333", result!.CivilNumber);
        Assert.Single(result.IdCards);
        Assert.Single(result.Passports);
        Assert.Equal("CARD-1", result.IdCards[0].CardNumber);
        Assert.Equal(CardStatus.ACTIVE, result.IdCards[0].Status);
        Assert.Equal("PASS-1", result.Passports[0].PassportNumber);
        Assert.Equal(PassportStatus.ACTIVE, result.Passports[0].Status);
    }

    // --- helpers ---------------------------------------------------------

    private static Person BuildPerson(
        string civilNumber,
        string firstNameEn = "First",
        PersonStatus status = PersonStatus.ACTIVE)
        => new()
        {
            CivilNumber = civilNumber,
            FirstNameAr = "الاسم",
            FamilyNameAr = "العائلة",
            FirstNameEn = firstNameEn,
            FamilyNameEn = "Family",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Gender = "M",
            NationalityCode = "OMN",
            Status = status,
        };

    private static IdCard BuildIdCard(
        string cardNumber,
        CardStatus status,
        CardType cardType)
        => new()
        {
            IdCardId = 1,
            CivilNumber = "33333333",
            CardNumber = cardNumber,
            IssueDate = new DateOnly(2020, 1, 1),
            ExpiryDate = new DateOnly(2030, 1, 1),
            Status = status,
            CardType = cardType,
        };

    private static Passport BuildPassport(
        string passportNumber,
        PassportType type,
        PassportStatus status)
        => new()
        {
            PassportId = 1,
            CivilNumber = "33333333",
            PassportNumber = passportNumber,
            PassportType = type,
            IssueDate = new DateOnly(2021, 1, 1),
            ExpiryDate = new DateOnly(2031, 1, 1),
            Status = status,
        };
}
