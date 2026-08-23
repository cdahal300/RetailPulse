# Multi-stage build for RetailPulse Cloud API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /build

# Copy solution and project files
COPY RetailPulse.sln .
COPY src/BuildingBlocks/RetailPulse.BuildingBlocks/RetailPulse.BuildingBlocks.csproj src/BuildingBlocks/RetailPulse.BuildingBlocks/
COPY src/Cloud/RetailPulse.Cloud/RetailPulse.Cloud.csproj src/Cloud/RetailPulse.Cloud/

# Restore dependencies
RUN dotnet restore RetailPulse.sln

# Copy source code
COPY src/ src/

# Build the Cloud API
RUN dotnet build src/Cloud/RetailPulse.Cloud/RetailPulse.Cloud.csproj -c Release --no-restore

# Publish
RUN dotnet publish src/Cloud/RetailPulse.Cloud/RetailPulse.Cloud.csproj -c Release -o /app --no-build --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=builder /app .

# Health check
HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD curl -f http://localhost:5000/health/live || exit 1

# Environment
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 5000
ENTRYPOINT ["dotnet", "RetailPulse.Cloud.dll"]
