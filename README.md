# Chivato - Azure Infrastructure Drift Monitor

Chivato is a SaaS application that monitors Azure infrastructure for configuration drift by comparing Terraform definitions with actual deployed resources.

## Architecture

Built with **Clean Architecture** and **CQRS** pattern using MediatR.

```
src/
├── core/
│   ├── Chivato.Domain/          # Entities, Value Objects, Interfaces
│   └── Chivato.Application/     # Commands, Queries, Handlers (MediatR)
├── infrastructure/
│   └── Chivato.Infrastructure/  # Azure Storage, Service Bus, SignalR
├── api/
│   └── Chivato.Api/             # ASP.NET Core Web API
├── worker/
│   └── Chivato.Worker/          # Background service (Service Bus consumer)
├── ui/                          # React + Vite frontend
└── devops/
    └── terraform/               # Azure Container Apps infrastructure
```

## Tech Stack

- **Backend**: .NET 10, ASP.NET Core Web API
- **Frontend**: React 19, Vite, TypeScript
- **Database**: Azure Table Storage
- **Messaging**: Azure Service Bus
- **Real-time**: Azure SignalR Service
- **Infrastructure**: Azure Container Apps, Terraform
- **CI/CD**: GitHub Actions

## Features

- 🔍 **Drift Detection**: Compare IaC definitions with live Azure resources
- 📊 **Dashboard**: Visualize drift severity and trends
- 🔔 **Real-time Notifications**: SignalR-powered progress updates
- 📧 **Email Alerts**: Configurable notifications for drift events
- 🔐 **Multi-tenant**: Azure AD authentication per tenant
- 📈 **Historical Analysis**: Track drift over time

## Getting Started

### Prerequisites

- .NET 10 SDK
- Node.js 22+
- Docker & Docker Compose
- Azure CLI (for deployment)

### Local Development

```bash
# Start infrastructure (Azurite, etc.)
docker-compose up -d

# Run API
cd src/api/Chivato.Api
dotnet run

# Run Worker
cd src/worker/Chivato.Worker
dotnet run

# Run UI
cd src/ui
npm install
npm run dev
```

### Configuration

Copy the example environment files:

```bash
cp src/ui/.env.example src/ui/.env
```

## Deployment

See [Terraform configuration](src/devops/terraform/README.md) for Azure Container Apps deployment.

## License

MIT
