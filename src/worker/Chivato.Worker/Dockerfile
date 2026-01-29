# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY shared/Chivato.Shared/Chivato.Shared.csproj shared/Chivato.Shared/
COPY worker/Chivato.Worker/Chivato.Worker.csproj worker/Chivato.Worker/

# Restore dependencies
RUN dotnet restore worker/Chivato.Worker/Chivato.Worker.csproj

# Copy source code
COPY shared/Chivato.Shared/ shared/Chivato.Shared/
COPY worker/Chivato.Worker/ worker/Chivato.Worker/

# Build
RUN dotnet build worker/Chivato.Worker/Chivato.Worker.csproj -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish worker/Chivato.Worker/Chivato.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Health check - worker exposes health endpoint via Microsoft.Extensions.Diagnostics.HealthChecks
HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD dotnet Chivato.Worker.dll --health-check || exit 1

COPY --from=publish /app/publish .

# Set environment
ENV DOTNET_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Chivato.Worker.dll"]
