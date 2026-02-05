# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files for restore
COPY shared/Chivato.Shared/Chivato.Shared.csproj shared/Chivato.Shared/
COPY worker/Chivato.IacWorker/Chivato.IacWorker.csproj worker/Chivato.IacWorker/

# Restore dependencies
RUN dotnet restore worker/Chivato.IacWorker/Chivato.IacWorker.csproj

# Copy source code
COPY shared/Chivato.Shared/ shared/Chivato.Shared/
COPY worker/Chivato.IacWorker/ worker/Chivato.IacWorker/

# Build
RUN dotnet build worker/Chivato.IacWorker/Chivato.IacWorker.csproj -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish worker/Chivato.IacWorker/Chivato.IacWorker.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage with IaC tools
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install required tools: git, curl, unzip
RUN apt-get update && apt-get install -y \
    git \
    curl \
    unzip \
    ca-certificates \
    gnupg \
    lsb-release \
    && rm -rf /var/lib/apt/lists/*

# Install Terraform
ARG TERRAFORM_VERSION=1.9.8
RUN curl -fsSL https://releases.hashicorp.com/terraform/${TERRAFORM_VERSION}/terraform_${TERRAFORM_VERSION}_linux_amd64.zip -o terraform.zip \
    && unzip terraform.zip -d /usr/local/bin/ \
    && rm terraform.zip \
    && terraform version

# Install Azure CLI (needed for some terraform authentication scenarios)
RUN curl -sL https://aka.ms/InstallAzureCLIDeb | bash \
    && az version

# Create working directory for IaC operations
RUN mkdir -p /tmp/chivato-iac && chmod 777 /tmp/chivato-iac

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV DOTNET_ENVIRONMENT=Development
ENV WorkingDirectory=/tmp/chivato-iac
ENV TF_INPUT=0
ENV TF_IN_AUTOMATION=true

# Create non-root user for security
RUN groupadd -r chivato && useradd -r -g chivato chivato
RUN chown -R chivato:chivato /app /tmp/chivato-iac
USER chivato

ENTRYPOINT ["dotnet", "Chivato.IacWorker.dll"]
