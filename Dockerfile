# ── Stage 1: Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first (for layer caching)
COPY GMAO.sln .
COPY GMAO.Domain/GMAO.Domain.csproj GMAO.Domain/
COPY GMAO.Application/GMAO.Application.csproj GMAO.Application/
COPY GMAO.Infrastructure/GMAO.Infrastructure.csproj GMAO.Infrastructure/
COPY GMAO.API/GMAO.API.csproj GMAO.API/

# Restore dependencies
RUN dotnet restore

# Copy all source code
COPY . .

# Build and publish
RUN dotnet publish GMAO.API/GMAO.API.csproj -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create uploads directory
RUN mkdir -p uploads

# Copy published files
COPY --from=build /app/publish .

# Expose port (Railway/Render use the PORT env variable)
ENV ASPNETCORE_URLS=http://+:${PORT:-5151}
EXPOSE 5151

ENTRYPOINT ["dotnet", "GMAO.API.dll"]
