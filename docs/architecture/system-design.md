# Diseño del Sistema - Chivato

## Diagrama de Componentes

```
┌────────────────────────────────────────────────────────────────────────────┐
│                              CHIVATO SYSTEM                                 │
├────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                        FRONTEND (Vite + React)                       │  │
│  │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌────────────┐  │  │
│  │  │ Dashboard    │ │ Config       │ │ Pipelines    │ │ Reports    │  │  │
│  │  │ Component    │ │ Component    │ │ Component    │ │ Component  │  │  │
│  │  └──────────────┘ └──────────────┘ └──────────────┘ └────────────┘  │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                    │                                       │
│                                    ▼                                       │
│  ┌─────────────────────────────────────────────────────────────────────┐  │
│  │                    AZURE FUNCTIONS (.NET 10)                         │  │
│  │                                                                      │  │
│  │  ┌─────────────────────────────────────────────────────────────┐    │  │
│  │  │                    HTTP TRIGGER FUNCTIONS                    │    │  │
│  │  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐            │    │  │
│  │  │  │ ConfigApi   │ │ PipelineApi │ │ ReportApi   │            │    │  │
│  │  │  └─────────────┘ └─────────────┘ └─────────────┘            │    │  │
│  │  └─────────────────────────────────────────────────────────────┘    │  │
│  │                                                                      │  │
│  │  ┌─────────────────────────────────────────────────────────────┐    │  │
│  │  │                   TIMER TRIGGER FUNCTION                     │    │  │
│  │  │  ┌──────────────────────────────────────────────────────┐   │    │  │
│  │  │  │               DriftAnalyzerFunction                   │   │    │  │
│  │  │  │  1. Load Config                                       │   │    │  │
│  │  │  │  2. Scan ADO Pipelines                                │   │    │  │
│  │  │  │  3. Inspect Azure Resources                           │   │    │  │
│  │  │  │  4. Analyze with AI                                   │   │    │  │
│  │  │  │  5. Store Results                                     │   │    │  │
│  │  │  │  6. Send Reports                                      │   │    │  │
│  │  │  └──────────────────────────────────────────────────────┘   │    │  │
│  │  └─────────────────────────────────────────────────────────────┘    │  │
│  │                                                                      │  │
│  │  ┌─────────────────────────────────────────────────────────────┐    │  │
│  │  │                        SERVICES                              │    │  │
│  │  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐            │    │  │
│  │  │  │ AdoService  │ │ AzureRM     │ │ AiAnalyzer  │            │    │  │
│  │  │  │             │ │ Service     │ │ Service     │            │    │  │
│  │  │  └─────────────┘ └─────────────┘ └─────────────┘            │    │  │
│  │  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐            │    │  │
│  │  │  │ Storage     │ │ Email       │ │ Config      │            │    │  │
│  │  │  │ Service     │ │ Service     │ │ Service     │            │    │  │
│  │  │  └─────────────┘ └─────────────┘ └─────────────┘            │    │  │
│  │  └─────────────────────────────────────────────────────────────┘    │  │
│  └─────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
└────────────────────────────────────────────────────────────────────────────┘
                │                    │                    │
                ▼                    ▼                    ▼
┌──────────────────────┐ ┌──────────────────┐ ┌──────────────────────────┐
│ Azure Table Storage  │ │ Azure AI Foundry │ │ Azure Communication Svc  │
│ ────────────────────│ │ ──────────────── │ │ ────────────────────────│
│ Tables:              │ │                  │ │                          │
│ - Configurations     │ │ GPT-5 Endpoint   │ │ Email Service            │
│ - Pipelines          │ │                  │ │                          │
│ - DriftRecords       │ │                  │ │                          │
│ - EmailRecipients    │ │                  │ │                          │
│ - AzureConnections   │ │                  │ │                          │
│ - AdoConnections     │ │                  │ │                          │
└──────────────────────┘ └──────────────────┘ └──────────────────────────┘
        │
        │ (secrets only)
        ▼
┌──────────────────────┐
│ Azure Key Vault      │
│ ──────────────────── │
│ - SP ClientSecrets   │
│ - ADO PATs           │
│ - AI API Keys        │
│ - ACS ConnStrings    │
└──────────────────────┘
```

## Modelo de Datos (Azure Table Storage)

### Tabla: Configurations

| PartitionKey | RowKey | Valor | Tipo | UpdatedAt |
|--------------|--------|-------|------|-----------|
| "config" | "timer_interval" | "24" | "hours" | timestamp |
| "config" | "retention_days" | "90" | "days" | timestamp |

### Tabla: Pipelines

| PartitionKey | RowKey | OrganizationUrl | ProjectName | PipelineName | IsActive | CreatedAt |
|--------------|--------|-----------------|-------------|--------------|----------|-----------|
| {org_id} | {pipeline_id} | "https://dev.azure.com/org" | "project" | "pipeline" | true | timestamp |

### Tabla: DriftRecords

| PartitionKey | RowKey | PipelineId | ResourceId | ResourceType | DriftType | Severity | Description | Recommendation | DetectedAt |
|--------------|--------|------------|------------|--------------|-----------|----------|-------------|----------------|------------|
| {date:yyyyMMdd} | {guid} | "pipe123" | "/subs/.../rg/res" | "Microsoft.Web/sites" | "PropertyMismatch" | "High" | "SKU changed" | "Update pipeline" | timestamp |

### Tabla: EmailRecipients

| PartitionKey | RowKey | Email | NotifyOn | IsActive | CreatedAt |
|--------------|--------|-------|----------|----------|-----------|
| "recipients" | {guid} | "user@email.com" | "always" | true | timestamp |

### Tabla: AzureConnections

| PartitionKey | RowKey | Name | TenantId | ClientId | KeyVaultSecretName | SubscriptionIds | Status | ExpiresAt | CreatedAt |
|--------------|--------|------|----------|----------|-------------------|-----------------|--------|-----------|-----------|
| "azure" | {guid} | "Produccion" | "tenant-guid" | "client-guid" | "azure-conn-{guid}" | ["sub1","sub2"] | "active" | timestamp | timestamp |

> **Nota**: `ClientSecret` NO se almacena aqui. Solo se guarda `KeyVaultSecretName` que es la referencia al secreto en Key Vault.

### Tabla: AdoConnections

| PartitionKey | RowKey | Name | OrganizationUrl | AuthType | KeyVaultSecretName | Status | ExpiresAt | CreatedAt |
|--------------|--------|------|-----------------|----------|-------------------|--------|-----------|-----------|
| "ado" | {guid} | "Mi Org ADO" | "https://dev.azure.com/miorg" | "PAT" | "ado-pat-{guid}" | "active" | timestamp | timestamp |

> **Nota**: `PAT` o `ClientSecret` NO se almacenan aqui. Solo referencia a Key Vault.

### Tabla: AiConnections

| PartitionKey | RowKey | Name | Endpoint | DeploymentName | AuthType | KeyVaultSecretName | Status | CreatedAt |
|--------------|--------|------|----------|----------------|----------|-------------------|--------|-----------|
| "ai" | {guid} | "GPT-5 Prod" | "https://xxx.openai.azure.com" | "gpt-5" | "ApiKey" | "ai-key-{guid}" | "active" | timestamp |

### Tabla: EmailServiceConfig

| PartitionKey | RowKey | KeyVaultSecretName | FromEmail | FromDisplayName | IsActive | CreatedAt |
|--------------|--------|-------------------|-----------|-----------------|----------|-----------|
| "email" | "primary" | "acs-connstring" | "noreply@midominio.com" | "Chivato Alerts" | true | timestamp |

## Flujo de Ejecucion del Timer

```
┌────────────────────────────────────────────────────────────────────────┐
│                    TIMER TRIGGER EXECUTION FLOW                         │
└────────────────────────────────────────────────────────────────────────┘

     ┌─────────────┐
     │   START     │
     │ (Timer)     │
     └──────┬──────┘
            │
            ▼
     ┌─────────────┐
     │ Load Config │
     │ from Table  │
     └──────┬──────┘
            │
            ▼
     ┌─────────────────┐
     │ Get Active      │
     │ Pipelines       │
     └────────┬────────┘
              │
              ▼
    ┌─────────────────────┐
    │ FOR EACH Pipeline   │◀────────────────────┐
    └─────────┬───────────┘                     │
              │                                 │
              ▼                                 │
    ┌─────────────────────┐                     │
    │ 1. Fetch Pipeline   │                     │
    │    YAML from ADO    │                     │
    └─────────┬───────────┘                     │
              │                                 │
              ▼                                 │
    ┌─────────────────────┐                     │
    │ 2. Parse IaC        │                     │
    │ (ARM/Bicep/TF)      │                     │
    └─────────┬───────────┘                     │
              │                                 │
              ▼                                 │
    ┌─────────────────────┐                     │
    │ 3. Get Azure        │                     │
    │    Resources State  │                     │
    └─────────┬───────────┘                     │
              │                                 │
              ▼                                 │
    ┌─────────────────────┐                     │
    │ 4. Send to AI       │                     │
    │    for Analysis     │                     │
    └─────────┬───────────┘                     │
              │                                 │
              ▼                                 │
    ┌─────────────────────┐                     │
    │ 5. Store Drift      │                     │
    │    Records          │                     │
    └─────────┬───────────┘                     │
              │                                 │
              ├──────────── More Pipelines? ────┘
              │ No
              ▼
    ┌─────────────────────┐
    │ 6. Generate Report  │
    └─────────┬───────────┘
              │
              ▼
    ┌─────────────────────┐
    │ 7. Send Email       │
    │    Notifications    │
    └─────────┬───────────┘
              │
              ▼
        ┌───────────┐
        │    END    │
        └───────────┘
```

## Estructura de Azure Functions

```
src/functions/
├── Chivato.Functions/
│   ├── Chivato.Functions.csproj
│   ├── Program.cs
│   ├── host.json
│   ├── local.settings.json
│   │
│   ├── Functions/
│   │   ├── DriftAnalyzerFunction.cs      # Timer trigger principal
│   │   ├── ConfigurationApi.cs           # HTTP API para config
│   │   ├── PipelineApi.cs                # HTTP API para pipelines
│   │   └── ReportApi.cs                  # HTTP API para reportes
│   │
│   ├── Services/
│   │   ├── IAdoService.cs
│   │   ├── AdoService.cs
│   │   ├── IAzureResourceService.cs
│   │   ├── AzureResourceService.cs
│   │   ├── IAiAnalyzerService.cs
│   │   ├── AiAnalyzerService.cs
│   │   ├── IStorageService.cs
│   │   ├── StorageService.cs
│   │   ├── IEmailService.cs
│   │   └── EmailService.cs
│   │
│   ├── Models/
│   │   ├── Configuration.cs
│   │   ├── Pipeline.cs
│   │   ├── DriftRecord.cs
│   │   ├── EmailRecipient.cs
│   │   ├── AzureResource.cs
│   │   └── DriftAnalysisResult.cs
│   │
│   └── Extensions/
│       └── ServiceCollectionExtensions.cs
│
└── Chivato.Functions.Tests/
    └── ...
```

## Estructura del Frontend

```
src/ui/
├── package.json
├── vite.config.ts
├── tsconfig.json
├── index.html
│
├── src/
│   ├── main.tsx
│   ├── App.tsx
│   │
│   ├── components/
│   │   ├── Layout/
│   │   ├── Dashboard/
│   │   ├── Configuration/
│   │   ├── Pipelines/
│   │   └── Reports/
│   │
│   ├── hooks/
│   │   ├── useConfig.ts
│   │   ├── usePipelines.ts
│   │   └── useDriftRecords.ts
│   │
│   ├── services/
│   │   └── api.ts
│   │
│   ├── types/
│   │   └── index.ts
│   │
│   └── styles/
│       └── globals.css
│
└── tests/
    └── ...
```
