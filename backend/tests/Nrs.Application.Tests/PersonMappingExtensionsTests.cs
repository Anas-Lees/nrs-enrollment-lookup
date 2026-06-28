using Nrs.Application.Mapping;
using Nrs.Domain.Entities;
using Nrs.Domain.Enums;

namespace Nrs.Application.Tests;

public class PersonMappingExtensionsTests
{
    [Fact]
    public void ToSummaryDto_MapsAllScalarFields()
    {
        var person = BuildPerson();

        var dto = person.ToSummaryDto();

        Assert.Equal(person.CivilNumber, dto.CivilNumber);
        Assert.Equal(person.FirstNameAr, dto.FirstNameAr);
        Assert.Equal(person.FamilyNameAr, dto.FamilyNameAr);
        Assert.Equal(person.FirstNameEn, dto.FirstNameEn);
        Assert.Equal(person.FamilyNameEn, dto.FamilyNameEn);
        Assert.Equal(person.DateOfBirth, dto.DateOfBirth);
        Assert.Equal(person.Gender, dto.Gender);
        Assert.Equal(person.NationalityCode, dto.NationalityCode);
        Assert.Equal(person.Status, dto.Status);
    }

    [Fact]
    public void ToDto_MapsPhotoAndDocuments()
    {
        var person = BuildPerson();
        person.PhotoPath = "photos/33333333.jpg";
        person.IdCards =
        [
            BuildIdCard(10, "CARD-A", CardStatus.ACTIVE, CardType.OMANI),
            BuildIdCard(11, "CARD-B", CardStatus.EXPIRED, CardType.RESIDENT),
        ];
        person.Passports =
        [
            BuildPassport(20, "PASS-A", PassportType.DIPLOMATIC, PassportStatus.ACTIVE),
        ];

        var dto = person.ToDto();

        Assert.Equal("photos/33333333.jpg", dto.PhotoPath);
        Assert.Equal(2, dto.IdCards.Count);
        Assert.Single(dto.Passports);

        var firstCard = dto.IdCards[0];
        Assert.Equal(10, firstCard.IdCardId);
        Assert.Equal("CARD-A", firstCard.CardNumber);
        Assert.Equal(CardStatus.ACTIVE, firstCard.Status);
        Assert.Equal(CardType.OMANI, firstCard.CardType);

        var secondCard = dto.IdCards[1];
        Assert.Equal(CardStatus.EXPIRED, secondCard.Status);
        Assert.Equal(CardType.RESIDENT, secondCard.CardType);

        var passport = dto.Passports[0];
        Assert.Equal(20, passport.PassportId);
        Assert.Equal("PASS-A", passport.PassportNumber);
        Assert.Equal(PassportType.DIPLOMATIC, passport.PassportType);
        Assert.Equal(PassportStatus.ACTIVE, passport.Status);
    }

    // --- helpers ---------------------------------------------------------

    private static Person BuildPerson()
        => new()
        {
            CivilNumber = "33333333",
            FirstNameAr = "سالم",
            FamilyNameAr = "البلوشي",
            FirstNameEn = "Salim",
            FamilyNameEn = "AlBalushi",
            DateOfBirth = new DateOnly(1985, 6, 15),
            Gender = "M",
            NationalityCode = "OMN",
            Status = PersonStatus.ACTIVE,
        };

    private static IdCard BuildIdCard(long id, string cardNumber, CardStatus status, CardType cardType)
        => new()
        {
            IdCardId = id,
            CivilNumber = "33333333",
            CardNumber = cardNumber,
            IssueDate = new DateOnly(2020, 1, 1),
            ExpiryDate = new DateOnly(2030, 1, 1),
            Status = status,
            CardType = cardType,
        };

    private static Passport BuildPassport(long id, string passportNumber, PassportType type, PassportStatus status)
        => new()
        {
            PassportId = id,
            CivilNumber = "33333333",
            PassportNumber = passportNumber,
            PassportType = type,
            IssueDate = new DateOnly(2021, 1, 1),
            ExpiryDate = new DateOnly(2031, 1, 1),
            Status = status,
        };
}
