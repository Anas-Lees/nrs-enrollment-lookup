using System.Globalization;
using System.Reflection;
using Nrs.Application.Dtos;
using Nrs.Domain.Enums;
using YamlDotNet.Serialization;

namespace Nrs.Contract.Tests;

/// <summary>
/// Verifies that the compiled backend (DTOs and enums) conforms to the frozen
/// OpenAPI contract at docs/api/openapi.yaml. The contract is the single source
/// of truth; these tests fail loudly if the code drifts from it.
/// </summary>
public class OpenApiContractTests
{
    private static readonly Dictionary<string, object> Root = LoadContract();

    /// <summary>
    /// Walks up from the test output directory until a folder contains
    /// docs/api/openapi.yaml, then parses it into a nested object model.
    /// </summary>
    private static Dictionary<string, object> LoadContract()
    {
        var path = LocateContractFile();
        var deserializer = new DeserializerBuilder().Build();
        var root = deserializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        Assert.NotNull(root);
        return root!;
    }

    private static string LocateContractFile()
    {
        const string relative = "docs/api/openapi.yaml";
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "api", "openapi.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Could not locate the frozen OpenAPI contract '{relative}' by walking up from " +
            $"'{AppContext.BaseDirectory}'. Ensure docs/api/openapi.yaml exists at the repo root.");
    }

    // -------------------- YAML navigation helpers --------------------

    private static Dictionary<object, object> AsMap(object? node)
    {
        Assert.NotNull(node);

        // YamlDotNet deserializes the root as Dictionary<string, object> (per the
        // explicit type argument) but nested maps as Dictionary<object, object>.
        // Normalise both to a Dictionary<object, object> for uniform navigation.
        switch (node)
        {
            case Dictionary<object, object> objMap:
                return objMap;
            case Dictionary<string, object> strMap:
                return strMap.ToDictionary(kv => (object)kv.Key, kv => kv.Value);
            default:
                Assert.Fail($"Expected a YAML mapping node but got '{node!.GetType().Name}'.");
                return null!; // unreachable
        }
    }

    private static object Get(Dictionary<object, object> map, string key)
    {
        Assert.True(map.ContainsKey(key), $"Expected key '{key}' in YAML node but it was missing.");
        return map[key]!;
    }

    private static Dictionary<object, object> GetSchema(string name)
    {
        var root = AsMap(Root);
        var components = AsMap(Get(root, "components"));
        var schemas = AsMap(Get(components, "schemas"));
        Assert.True(schemas.ContainsKey(name), $"Schema '{name}' is not defined in the contract.");
        return AsMap(schemas[name]);
    }

    private static List<string> GetEnumValues(string schemaName)
    {
        var schema = GetSchema(schemaName);
        Assert.True(schema.ContainsKey("enum"), $"Schema '{schemaName}' has no 'enum' member.");
        var values = schema["enum"] as IEnumerable<object>;
        Assert.True(values is not null, $"Schema '{schemaName}' 'enum' is not a sequence.");
        return values!.Select(v => v?.ToString() ?? string.Empty).ToList();
    }

    private static IEnumerable<string> GetSchemaPropertyNames(string schemaName)
    {
        var schema = GetSchema(schemaName);
        var properties = AsMap(Get(schema, "properties"));
        return properties.Keys.Select(k => k?.ToString() ?? string.Empty);
    }

    private static string ToCamelCase(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
        {
            return pascal;
        }

        return char.ToLower(pascal[0], CultureInfo.InvariantCulture) + pascal[1..];
    }

    private static HashSet<string> DtoPropertyNamesCamelCase(Type dtoType)
    {
        var names = dtoType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => ToCamelCase(p.Name));
        return new HashSet<string>(names, StringComparer.Ordinal);
    }

    private static string Diff(HashSet<string> expected, HashSet<string> actual)
    {
        var missing = expected.Except(actual).OrderBy(x => x, StringComparer.Ordinal);
        var extra = actual.Except(expected).OrderBy(x => x, StringComparer.Ordinal);
        return $"missing from actual: [{string.Join(", ", missing)}]; unexpected in actual: [{string.Join(", ", extra)}]";
    }

    // -------------------- Tests --------------------

    [Fact]
    public void Paths_DefineSearchAndProfileEndpoints()
    {
        var root = AsMap(Root);
        var paths = AsMap(Get(root, "paths"));
        var keys = paths.Keys.Select(k => k?.ToString()).ToHashSet(StringComparer.Ordinal);

        Assert.True(keys.Contains("/api/v1/persons/search"), "Contract is missing path '/api/v1/persons/search'.");
        Assert.True(keys.Contains("/api/v1/persons/{crn}"), "Contract is missing path '/api/v1/persons/{crn}'.");
    }

    [Theory]
    [InlineData("PersonStatus", typeof(PersonStatus))]
    [InlineData("CardStatus", typeof(CardStatus))]
    [InlineData("CardType", typeof(CardType))]
    [InlineData("PassportType", typeof(PassportType))]
    [InlineData("PassportStatus", typeof(PassportStatus))]
    [InlineData("MaritalStatus", typeof(MaritalStatus))]
    public void Enum_MatchesContract(string schemaName, Type enumType)
    {
        var expected = new HashSet<string>(GetEnumValues(schemaName), StringComparer.Ordinal);
        var actual = new HashSet<string>(Enum.GetNames(enumType), StringComparer.Ordinal);

        Assert.True(
            expected.SetEquals(actual),
            $"Enum '{enumType.Name}' does not match contract schema '{schemaName}': {Diff(expected, actual)}");
    }

    [Fact]
    public void Schema_PersonSummary_MatchesDtoProperties()
    {
        var expected = new HashSet<string>(GetSchemaPropertyNames("PersonSummary"), StringComparer.Ordinal);
        var actual = DtoPropertyNamesCamelCase(typeof(PersonSummaryDto));

        Assert.True(
            expected.SetEquals(actual),
            $"Schema 'PersonSummary' does not match PersonSummaryDto: {Diff(expected, actual)}");
    }

    [Fact]
    public void Schema_IdCard_MatchesDtoProperties()
    {
        var expected = new HashSet<string>(GetSchemaPropertyNames("IdCard"), StringComparer.Ordinal);
        var actual = DtoPropertyNamesCamelCase(typeof(IdCardDto));

        Assert.True(
            expected.SetEquals(actual),
            $"Schema 'IdCard' does not match IdCardDto: {Diff(expected, actual)}");
    }

    [Fact]
    public void Schema_Passport_MatchesDtoProperties()
    {
        var expected = new HashSet<string>(GetSchemaPropertyNames("Passport"), StringComparer.Ordinal);
        var actual = DtoPropertyNamesCamelCase(typeof(PassportDto));

        Assert.True(
            expected.SetEquals(actual),
            $"Schema 'Passport' does not match PassportDto: {Diff(expected, actual)}");
    }

    [Fact]
    public void Schema_Address_MatchesDtoProperties()
    {
        var expected = new HashSet<string>(GetSchemaPropertyNames("Address"), StringComparer.Ordinal);
        var actual = DtoPropertyNamesCamelCase(typeof(AddressDto));

        Assert.True(
            expected.SetEquals(actual),
            $"Schema 'Address' does not match AddressDto: {Diff(expected, actual)}");
    }

    [Fact]
    public void Schema_Contact_MatchesDtoProperties()
    {
        var expected = new HashSet<string>(GetSchemaPropertyNames("Contact"), StringComparer.Ordinal);
        var actual = DtoPropertyNamesCamelCase(typeof(ContactDto));

        Assert.True(
            expected.SetEquals(actual),
            $"Schema 'Contact' does not match ContactDto: {Diff(expected, actual)}");
    }

    [Fact]
    public void Schema_Person_MatchesDtoProperties()
    {
        // The full Person contract is PersonSummary's properties plus everything declared
        // inline across the allOf entries; together they must equal PersonDto exactly.
        var expected = new HashSet<string>(GetSchemaPropertyNames("PersonSummary"), StringComparer.Ordinal);
        var schema = GetSchema("Person");
        var allOf = schema["allOf"] as IEnumerable<object>;
        Assert.True(allOf is not null, "Schema 'Person' 'allOf' is not a sequence.");
        foreach (var entry in allOf!)
        {
            if (entry is Dictionary<object, object> entryMap && entryMap.TryGetValue("properties", out var props))
            {
                foreach (var key in AsMap(props).Keys)
                {
                    expected.Add(key?.ToString() ?? string.Empty);
                }
            }
        }

        var actual = DtoPropertyNamesCamelCase(typeof(PersonDto));

        Assert.True(
            expected.SetEquals(actual),
            $"Schema 'Person' does not match PersonDto: {Diff(expected, actual)}");
    }

    [Fact]
    public void Schema_Person_DeclaresPhotoAndDocuments()
    {
        var schema = GetSchema("Person");
        Assert.True(schema.ContainsKey("allOf"), "Schema 'Person' is expected to use 'allOf'.");

        var allOf = schema["allOf"] as IEnumerable<object>;
        Assert.True(allOf is not null, "Schema 'Person' 'allOf' is not a sequence.");

        // Collect every property name declared across all allOf entries.
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in allOf!)
        {
            if (entry is Dictionary<object, object> entryMap && entryMap.TryGetValue("properties", out var props))
            {
                foreach (var key in AsMap(props).Keys)
                {
                    declared.Add(key?.ToString() ?? string.Empty);
                }
            }
        }

        foreach (var required in new[] { "photoPath", "idCards", "passports" })
        {
            Assert.True(
                declared.Contains(required),
                $"Schema 'Person' allOf must declare property '{required}'. Declared: [{string.Join(", ", declared.OrderBy(x => x, StringComparer.Ordinal))}]");
        }
    }
}
