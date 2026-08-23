# Multi-stage build for RetailPulse Edge API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /build

# Copy solution and project files
COPY RetailPulse.sln .
COPY src/BuildingBlocks/RetailPulse.BuildingBlocks/RetailPulse.BuildingBlocks.csproj src/BuildingBlocks/RetailPulse.BuildingBlocks/
COPY src/Edge/RetailPulse.Edge/RetailPulse.Edge.csproj src/Edge/RetailPulse.Edge/

# Restore dependencies
RUN dotnet restore RetailPulse.sln

# Copy source code
COPY src/ src/

# Build the Edge API
RUN dotnet build src/Edge/RetailPulse.Edge/RetailPulse.Edge.csproj -c Release --no-restore

# Publish
RUN dotnet publish src/Edge/RetailPulse.Edge/RetailPulse.Edge.csproj -c Release -o /app --no-build --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=builder /app .

# Create directory for SQLite database
RUN mkdir -p /data

# Health check
HEALTHCHECK --interval=10s --timeout=5s --retries=3 \
    CMD curl -f http://localhost:5000/health/ready || exit 1

# Environment
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV RetailPulse__EdgeDatabasePath=/data/retailpulse-edge.db

EXPOSE 5000
ENTRYPOINT ["dotnet", "RetailPulse.Edge.dll"]
