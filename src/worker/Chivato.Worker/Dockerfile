# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY core/Chivato.Domain/Chivato.Domain.csproj core/Chivato.Domain/
COPY core/Chivato.Application/Chivato.Application.csproj core/Chivato.Application/
COPY core/Chivato.Infrastructure/Chivato.Infrastructure.csproj core/Chivato.Infrastructure/
COPY infrastructure/Chivato.Infrastructure/Chivato.Infrastructure.csproj infrastructure/Chivato.Infrastructure/
COPY shared/Chivato.Shared/Chivato.Shared.csproj shared/Chivato.Shared/
COPY worker/Chivato.Worker/Chivato.Worker.csproj worker/Chivato.Worker/

# Restore dependencies
RUN dotnet restore worker/Chivato.Worker/Chivato.Worker.csproj

# Copy source code
COPY core/Chivato.Domain/ core/Chivato.Domain/
COPY core/Chivato.Application/ core/Chivato.Application/
COPY core/Chivato.Infrastructure/ core/Chivato.Infrastructure/
COPY infrastructure/Chivato.Infrastructure/ infrastructure/Chivato.Infrastructure/
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

COPY --from=publish /app/publish .

# Set environment
ENV DOTNET_ENVIRONMENT=Development

ENTRYPOINT ["dotnet", "Chivato.Worker.dll"]
