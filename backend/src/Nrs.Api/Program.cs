var builder = WebApplication.CreateBuilder(args);

// Service registration is fleshed out in later tasks (DI, Swagger, CORS, EF Core).

var app = builder.Build();

// Liveness placeholder until the real endpoints and middleware land.
app.MapGet("/", () => "NRS Enrollment — Applicant Lookup API. Swagger UI added in a later task.");

app.Run();

// Exposed so the integration-test project (WebApplicationFactory<Program>) can reference it.
public partial class Program;
