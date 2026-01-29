# ==========================================
# Chivato Functions - Custom Runtime
# ==========================================
# Using .NET SDK with Azure Functions Core Tools
# Compatible with ARM64 (Apple Silicon)
# ==========================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all source files first
COPY . .

# Modify target framework for Docker build (net10 -> net8)
RUN sed -i 's/net10.0/net8.0/g' Chivato.Functions.csproj

# Restore and build
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Install Azure Functions Core Tools
RUN apt-get update && \
    apt-get install -y wget apt-transport-https software-properties-common && \
    wget -q https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb && \
    dpkg -i packages-microsoft-prod.deb && \
    rm packages-microsoft-prod.deb && \
    apt-get update && \
    apt-get install -y azure-functions-core-tools-4 && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=build /app/publish .

# Create host.json if not exists
RUN test -f host.json || echo '{"version": "2.0", "logging": {"logLevel": {"default": "Information"}}}' > host.json

# Environment variables
ENV AzureWebJobsScriptRoot=/app \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
    FUNCTIONS_CORE_TOOLS_TELEMETRY_OPTOUT=1

EXPOSE 80

# Start Azure Functions host
CMD ["func", "start", "--port", "80"]
