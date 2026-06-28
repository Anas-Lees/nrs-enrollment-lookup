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
COPY --from=build /app/publish .

# Writable data dir for the local SQLite database (POC). Made group-0 writable so
# it works both for the base image's non-root user and under OpenShift's
# arbitrary-UID model (random uid, group 0).
RUN mkdir -p /app/data && chown -R $APP_UID:0 /app/data && chmod -R g=u /app/data

# Run as the non-root user the base image provides.
USER $APP_UID

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/app/data/nrs.db"
ENTRYPOINT ["dotnet", "Nrs.Api.dll"]
