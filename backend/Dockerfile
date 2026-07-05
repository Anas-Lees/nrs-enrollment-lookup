# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the solution and restore (separate layer for better caching).
COPY . .
RUN dotnet restore Nrs.ApplicantLookup.slnx

# Publish the API (and its referenced projects) in Release.
RUN dotnet publish src/Nrs.Api/Nrs.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl for the container HEALTHCHECK — the aspnet runtime image ships neither curl nor
# wget. Installed before the app copy so this layer caches across code changes.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Run as the non-root user the base image provides.
USER $APP_UID

EXPOSE 8080
# The Oracle connection string is supplied at runtime (docker-compose / OpenShift secret).
ENV ASPNETCORE_URLS=http://+:8080

# Container-level liveness signal (orchestrators that honour HEALTHCHECK; also used by
# docker-compose to gate the SPA on a ready API). Hits the anonymous readiness endpoint.
HEALTHCHECK --interval=30s --timeout=3s --start-period=30s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health/ready || exit 1

ENTRYPOINT ["dotnet", "Nrs.Api.dll"]
