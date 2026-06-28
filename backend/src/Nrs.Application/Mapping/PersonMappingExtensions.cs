using Nrs.Application.Dtos;
using Nrs.Domain.Entities;

namespace Nrs.Application.Mapping;

/// <summary>
/// Manual entity → DTO mapping. Chosen over a mapping library so the translation is
/// explicit, dependency-free, and AOT/trim-friendly (the brief permits manual mapping).
/// </summary>
public static class PersonMappingExtensions
{
    public static PersonSummaryDto ToSummaryDto(this Person person) => new()
    {
        CivilNumber = person.CivilNumber,
        FirstNameAr = person.FirstNameAr,
        FamilyNameAr = person.FamilyNameAr,
        FirstNameEn = person.FirstNameEn,
        FamilyNameEn = person.FamilyNameEn,
        DateOfBirth = person.DateOfBirth,
        Gender = person.Gender,
        NationalityCode = person.NationalityCode,
        NationalityNameEn = person.Nationality?.NameEn,
        NationalityNameAr = person.Nationality?.NameAr,
        Status = person.Status,
    };

    public static PersonDto ToDto(this Person person) => new()
    {
        CivilNumber = person.CivilNumber,
        FirstNameAr = person.FirstNameAr,
        FamilyNameAr = person.FamilyNameAr,
        FirstNameEn = person.FirstNameEn,
        FamilyNameEn = person.FamilyNameEn,
        DateOfBirth = person.DateOfBirth,
        Gender = person.Gender,
        NationalityCode = person.NationalityCode,
        NationalityNameEn = person.Nationality?.NameEn,
        NationalityNameAr = person.Nationality?.NameAr,
        Status = person.Status,
        PhotoPath = person.PhotoPath,
        PlaceOfBirthEn = person.PlaceOfBirthEn,
        PlaceOfBirthAr = person.PlaceOfBirthAr,
        MotherNameEn = person.MotherNameEn,
        MotherNameAr = person.MotherNameAr,
        MaritalStatus = person.MaritalStatus,
        BloodType = person.BloodType,
        OccupationEn = person.OccupationEn,
        OccupationAr = person.OccupationAr,
        Address = person.Address?.ToDto(),
        Contact = person.Contact?.ToDto(),
        IdCards = person.IdCards.Select(card => card.ToDto()).ToList(),
        Passports = person.Passports.Select(passport => passport.ToDto()).ToList(),
    };

    public static AddressDto ToDto(this Address address) => new()
    {
        Governorate = address.Governorate,
        Wilayat = address.Wilayat,
        Village = address.Village,
        Street = address.Street,
        BuildingNumber = address.BuildingNumber,
        PostalCode = address.PostalCode,
    };

    public static ContactDto ToDto(this Contact contact) => new()
    {
        Mobile = contact.Mobile,
        Email = contact.Email,
    };

    public static IdCardDto ToDto(this IdCard card) => new()
    {
        IdCardId = card.IdCardId,
        CivilNumber = card.CivilNumber,
        CardNumber = card.CardNumber,
        IssueDate = card.IssueDate,
        ExpiryDate = card.ExpiryDate,
        Status = card.Status,
        CardType = card.CardType,
    };

    public static PassportDto ToDto(this Passport passport) => new()
    {
        PassportId = passport.PassportId,
        CivilNumber = passport.CivilNumber,
        PassportNumber = passport.PassportNumber,
        PassportType = passport.PassportType,
        IssueDate = passport.IssueDate,
        ExpiryDate = passport.ExpiryDate,
        Status = passport.Status,
    };
}
