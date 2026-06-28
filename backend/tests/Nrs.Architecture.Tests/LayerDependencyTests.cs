using System.Reflection;
using NetArchTest.Rules;

namespace Nrs.Architecture.Tests;

/// <summary>
/// Enforces the Clean Architecture layering rules of the NRS Enrollment backend.
/// Inner layers must not depend on outer layers, and no domain/application code
/// may leak framework dependencies (EF Core, ASP.NET Core).
/// </summary>
public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly =
        typeof(Nrs.Domain.Entities.Person).Assembly;

    private static readonly Assembly ApplicationAssembly =
        typeof(Nrs.Application.Interfaces.IPersonRepository).Assembly;

    private static readonly Assembly InfrastructureAssembly =
        typeof(Nrs.Infrastructure.Persistence.NrsDbContext).Assembly;

    private static readonly Assembly ApiAssembly =
        typeof(Nrs.Api.Controllers.PersonLookupController).Assembly;

    [Fact]
    public void Domain_HasNoDependencyOnOtherLayersOrFrameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should().NotHaveDependencyOnAny(
                "Nrs.Application",
                "Nrs.Infrastructure",
                "Nrs.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should().NotHaveDependencyOnAny(
                "Nrs.Infrastructure",
                "Nrs.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should().NotHaveDependencyOn("Nrs.Api")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>()));
    }

    [Fact]
    public void Controllers_DoNotUseEfCoreOrInfrastructurePersistence()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("Nrs.Api.Controllers")
            .ShouldNot().HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Nrs.Infrastructure.Persistence")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "Offending types: " + string.Join(", ", result.FailingTypeNames ?? Enumerable.Empty<string>()));
    }
}
