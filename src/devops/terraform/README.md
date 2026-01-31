# Chivato - Terraform Infrastructure

This directory contains the Terraform configuration for deploying Chivato infrastructure to Azure.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Azure Container Apps                         │
│  ┌─────────────────┐              ┌─────────────────┐           │
│  │   API Container │◄────────────►│ Worker Container│           │
│  │   (ASP.NET)     │              │  (.NET Worker)  │           │
│  └────────┬────────┘              └────────┬────────┘           │
│           │                                │                     │
└───────────┼────────────────────────────────┼─────────────────────┘
            │                                │
    ┌───────┴───────┐                ┌───────┴───────┐
    │  Azure SignalR │                │ Service Bus   │
    │  (Real-time)   │                │ (Queue)       │
    └───────────────┘                └───────────────┘
            │                                │
    ┌───────┴────────────────────────────────┴───────┐
    │                                                 │
    │  ┌──────────┐  ┌──────────┐  ┌──────────────┐ │
    │  │ Key Vault│  │ Storage  │  │ App Insights │ │
    │  │ (Secrets)│  │ (Tables) │  │ (Monitoring) │ │
    │  └──────────┘  └──────────┘  └──────────────┘ │
    │                                                 │
    └─────────────────────────────────────────────────┘
```

## Prerequisites

- [Terraform](https://www.terraform.io/downloads) >= 1.5.0
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli) authenticated
- Access to the Azure subscription

## Quick Start

1. **Login to Azure**
   ```bash
   az login
   az account set --subscription "ec654428-81d7-4dcd-8ba8-5b8f632bec29"
   ```

2. **Initialize Terraform**
   ```bash
   make init
   ```

3. **Plan changes**
   ```bash
   make plan ENV=dev
   ```

4. **Apply changes**
   ```bash
   make apply ENV=dev
   ```

## Environment Configuration

Environment-specific variables are in `environments/`:

- `dev.tfvars` - Development environment
- `staging.tfvars` - Staging environment (create when needed)
- `prod.tfvars` - Production environment (create when needed)

## Importing Existing Resources

The `import.tf` file contains import blocks for existing Azure resources. When running `terraform plan`, Terraform will automatically import these resources into its state.

Existing resources in `rg-chivato-dev`:
- Resource Group: `rg-chivato-dev`
- Storage Account: `stchivatodev`
- Key Vault: `kv-chivato-dev`
- Application Insights: `appi-chivato-dev`

## Backend State

Terraform state is stored in Azure Storage:

- **Storage Account**: `kodepstr`
- **Container**: `tfstate`
- **State File**: `chivato-dev.tfstate`

## Resources Created

| Resource | Name Pattern | Description |
|----------|--------------|-------------|
| Resource Group | `rg-chivato-{env}` | Contains all resources |
| Storage Account | `stchivato{env}` | Table storage for data |
| Key Vault | `kv-chivato-{env}` | Secrets management |
| App Insights | `appi-chivato-{env}` | Application monitoring |
| Container Registry | `acrchivato{env}` | Docker images |
| Container Apps Env | `cae-chivato-{env}` | Container runtime |
| Container App API | `ca-chivato-{env}-api` | API container |
| Container App Worker | `ca-chivato-{env}-worker` | Background worker |
| Service Bus | `sb-chivato-{env}` | Message queue |
| SignalR | `sigr-chivato-{env}` | Real-time notifications |

## Makefile Commands

```bash
make help         # Show all commands
make init         # Initialize Terraform
make plan         # Show planned changes
make apply        # Apply changes
make destroy      # Destroy resources (DANGER!)
make fmt          # Format Terraform files
make validate     # Validate configuration
make output       # Show outputs
make state-list   # List resources in state
```

## Outputs

After applying, you can get important values:

```bash
terraform output api_url           # API endpoint URL
terraform output acr_login_server  # Container Registry URL
```

## CI/CD Pipeline

The infrastructure is deployed via Azure DevOps Pipeline. See `.azure-pipelines/terraform.yml`.

## Troubleshooting

### "Backend configuration changed"
```bash
terraform init -reconfigure
```

### "Resource already exists"
The import blocks in `import.tf` should handle this. If not:
```bash
terraform import azurerm_resource_group.main /subscriptions/.../resourceGroups/rg-chivato-dev
```

### "Permission denied"
Ensure you have Owner or Contributor role on the subscription.
