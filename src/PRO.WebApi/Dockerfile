# ── Build stage ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY PRO.sln ./
COPY src/PRO.Domain/PRO.Domain.csproj src/PRO.Domain/
COPY src/PRO.Application/PRO.Application.csproj src/PRO.Application/
COPY src/PRO.Infrastructure/PRO.Infrastructure.csproj src/PRO.Infrastructure/
COPY src/PRO.WebApi/PRO.WebApi.csproj src/PRO.WebApi/

# Restore
RUN dotnet restore src/PRO.WebApi/PRO.WebApi.csproj

# Copy all source
COPY src/ src/

# Publish
RUN dotnet publish src/PRO.WebApi/PRO.WebApi.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ── Runtime stage ─────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos '' appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "PRO.WebApi.dll"]
