using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Nrs.Api.OpenApi;

/// <summary>
/// Enriches the generated OpenAPI document: sets the API Info (title/version/description) and
/// adds an HTTP "bearer" (JWT) security scheme applied as a top-level requirement. The scheme is
/// what makes Scalar render an "Authorize" button and attach the token to test calls — without it
/// every request from the docs page hits the Keycloak-protected endpoints unauthenticated (401).
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string SchemeId = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info ??= new OpenApiInfo();
        document.Info.Title = "NRS Enrollment — Applicant Lookup API";
        document.Info.Version = "v1";
        document.Info.Description =
            "Read-only applicant lookup over the National Registration System (NRS). "
            + "Search applicants and retrieve full profiles — biographic data, addresses, "
            + "contacts, ID cards and passports — plus the lookup audit trail.\n\n"
            + "**Auth:** every endpoint requires a Keycloak JWT with the `operator` realm role "
            + "(when `Auth:Enabled` is true). Click **Authorize**, paste a bearer token, then try a "
            + "request. Get a token by signing in to the web app and copying the access token.";
        document.Info.Contact = new OpenApiContact
        {
            Name = "Royal Oman Police — NRS Platform",
            Url = new Uri("https://github.com/Anas-Lees/nrs-enrollment-lookup"),
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer", // lower-case per the OpenAPI/HTTP spec
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste a Keycloak-issued JWT access token. Do NOT include the word 'Bearer'.",
        };

        // Apply the scheme as a top-level requirement so every operation shows the lock icon and
        // Scalar sends the token. The requirement key is a reference to the scheme by id (v2 API).
        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeId, document)] = new List<string>(),
        });

        return Task.CompletedTask;
    }
}
