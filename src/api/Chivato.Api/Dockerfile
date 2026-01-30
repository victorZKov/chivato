# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files (Clean Architecture)
COPY core/Chivato.Domain/Chivato.Domain.csproj core/Chivato.Domain/
COPY core/Chivato.Application/Chivato.Application.csproj core/Chivato.Application/
COPY core/Chivato.Infrastructure/Chivato.Infrastructure.csproj core/Chivato.Infrastructure/
COPY api/Chivato.Api/Chivato.Api.csproj api/Chivato.Api/

# Restore dependencies
RUN dotnet restore api/Chivato.Api/Chivato.Api.csproj

# Copy source code
COPY core/Chivato.Domain/ core/Chivato.Domain/
COPY core/Chivato.Application/ core/Chivato.Application/
COPY core/Chivato.Infrastructure/ core/Chivato.Infrastructure/
COPY api/Chivato.Api/ api/Chivato.Api/

# Build
RUN dotnet build api/Chivato.Api/Chivato.Api.csproj -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish api/Chivato.Api/Chivato.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Install curl for health check
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish /app/publish .

# Set environment
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

EXPOSE 8080

ENTRYPOINT ["dotnet", "Chivato.Api.dll"]
