terraform {
  required_version = ">= 1.5.0"

  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 3.7"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.85"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  backend "azurerm" {
    resource_group_name  = "rg-kovimatic-core"
    storage_account_name = "kodepstr"
    container_name       = "tfstate"
    key                  = "chivato-dev.tfstate"
  }
}

provider "azuread" {
  tenant_id = var.tenant_id
}

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy    = false
      recover_soft_deleted_key_vaults = true
    }
  }
  tenant_id       = var.tenant_id
  subscription_id = var.subscription_id
}

# Data sources
data "azuread_client_config" "current" {}

data "azurerm_client_config" "current" {}

# Local values
locals {
  resource_prefix = "chivato-${var.environment}"
  common_tags = {
    Application = "Chivato"
    Environment = var.environment
    ManagedBy   = "Terraform"
  }
}

# App Registration Module
module "app_registration" {
  source = "./modules/app-registration"

  app_name          = "Chivato - ${title(var.environment)}"
  environment       = var.environment
  tenant_id         = var.tenant_id
  spa_redirect_uris = var.spa_redirect_uris
  admin_user_ids    = var.admin_user_ids
  user_user_ids     = var.user_user_ids
  api_permissions   = var.api_permissions
}

# Resource Group
resource "azurerm_resource_group" "main" {
  name     = "rg-${local.resource_prefix}"
  location = var.location
  tags     = local.common_tags
}

# Storage Account for Table Storage
resource "azurerm_storage_account" "main" {
  name                     = replace("st${local.resource_prefix}", "-", "")
  resource_group_name      = azurerm_resource_group.main.name
  location                 = azurerm_resource_group.main.location
  account_tier             = "Standard"
  account_replication_type = var.environment == "prod" ? "GRS" : "LRS"
  min_tls_version          = "TLS1_2"

  tags = local.common_tags
}

# Create Tables
resource "azurerm_storage_table" "tables" {
  for_each             = toset(var.storage_tables)
  name                 = each.value
  storage_account_name = azurerm_storage_account.main.name
}

# Key Vault
resource "azurerm_key_vault" "main" {
  name                       = "kv-${local.resource_prefix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  tenant_id                  = var.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 7
  purge_protection_enabled   = var.environment == "prod"
  enable_rbac_authorization  = true

  tags = local.common_tags
}

# Key Vault access for current user (for initial setup)
resource "azurerm_role_assignment" "kv_admin" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Administrator"
  principal_id         = data.azurerm_client_config.current.object_id
}

# Key Vault access for the Function App (via managed identity)
resource "azurerm_role_assignment" "kv_secrets_user" {
  count                = var.create_function_app ? 1 : 0
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_function_app.main[0].identity[0].principal_id
}

# Application Insights
resource "azurerm_application_insights" "main" {
  name                = "appi-${local.resource_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  application_type    = "web"

  tags = local.common_tags

  lifecycle {
    ignore_changes = [workspace_id]
  }
}

# App Service Plan for Function App
resource "azurerm_service_plan" "main" {
  count               = var.create_function_app ? 1 : 0
  name                = "asp-${local.resource_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  os_type             = "Linux"
  sku_name            = var.function_app_sku

  tags = local.common_tags
}

# Function App
resource "azurerm_linux_function_app" "main" {
  count               = var.create_function_app ? 1 : 0
  name                = "func-${local.resource_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  service_plan_id     = azurerm_service_plan.main[0].id

  storage_account_name       = azurerm_storage_account.main.name
  storage_account_access_key = azurerm_storage_account.main.primary_access_key

  identity {
    type = "SystemAssigned"
  }

  site_config {
    application_stack {
      dotnet_version              = "10.0"
      use_dotnet_isolated_runtime = true
    }

    cors {
      allowed_origins     = var.cors_allowed_origins
      support_credentials = true
    }

    application_insights_key               = azurerm_application_insights.main.instrumentation_key
    application_insights_connection_string = azurerm_application_insights.main.connection_string
  }

  app_settings = {
    "FUNCTIONS_WORKER_RUNTIME" = "dotnet-isolated"
    "StorageConnectionString"  = azurerm_storage_account.main.primary_connection_string
    "KeyVaultUrl"              = azurerm_key_vault.main.vault_uri
    "AzureAd__TenantId"        = var.tenant_id
    "AzureAd__ClientId"        = module.app_registration.client_id
    "AzureAd__Audience"        = "api://${module.app_registration.client_id}"
  }

  tags = local.common_tags
}

# Communication Services (for email)
resource "azurerm_communication_service" "main" {
  count               = var.create_communication_service ? 1 : 0
  name                = "acs-${local.resource_prefix}"
  resource_group_name = azurerm_resource_group.main.name
  data_location       = "Europe"

  tags = local.common_tags
}

# Azure OpenAI (if needed in this subscription)
resource "azurerm_cognitive_account" "openai" {
  count               = var.create_openai ? 1 : 0
  name                = "oai-${local.resource_prefix}"
  location            = var.openai_location
  resource_group_name = azurerm_resource_group.main.name
  kind                = "OpenAI"
  sku_name            = "S0"

  tags = local.common_tags
}

resource "azurerm_cognitive_deployment" "gpt" {
  count                = var.create_openai ? 1 : 0
  name                 = "gpt-5"
  cognitive_account_id = azurerm_cognitive_account.openai[0].id

  model {
    format  = "OpenAI"
    name    = var.openai_model_name
    version = var.openai_model_version
  }

  scale {
    type     = "Standard"
    capacity = var.openai_capacity
  }
}

# ==========================================
# Azure Container Registry
# ==========================================

resource "azurerm_container_registry" "main" {
  count               = var.create_container_apps ? 1 : 0
  name                = replace("acr${local.resource_prefix}", "-", "")
  resource_group_name = azurerm_resource_group.main.name
  location            = azurerm_resource_group.main.location
  sku                 = var.environment == "prod" ? "Standard" : "Basic"
  admin_enabled       = true

  tags = local.common_tags
}

# ==========================================
# Azure Service Bus
# ==========================================

resource "azurerm_servicebus_namespace" "main" {
  count               = var.create_service_bus ? 1 : 0
  name                = "sb-${local.resource_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = var.service_bus_sku

  tags = local.common_tags
}

resource "azurerm_servicebus_queue" "drift_analysis" {
  count        = var.create_service_bus ? 1 : 0
  name         = "drift-analysis-requests"
  namespace_id = azurerm_servicebus_namespace.main[0].id

  # Enable dead-lettering
  dead_lettering_on_message_expiration = true

  # Duplicate detection (10 min window)
  requires_duplicate_detection         = var.service_bus_sku != "Basic" ? true : false
  duplicate_detection_history_time_window = var.service_bus_sku != "Basic" ? "PT10M" : null

  # Message settings
  max_delivery_count = 3
  lock_duration      = "PT5M"
  default_message_ttl = "P1D"
}

# ==========================================
# Azure SignalR Service
# ==========================================

resource "azurerm_signalr_service" "main" {
  count               = var.create_signalr ? 1 : 0
  name                = "sigr-${local.resource_prefix}"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  sku {
    name     = var.signalr_sku
    capacity = var.signalr_capacity
  }

  cors {
    allowed_origins = var.cors_allowed_origins
  }

  connectivity_logs_enabled = var.environment == "prod"
  messaging_logs_enabled    = var.environment == "prod"
  service_mode              = "Default"

  tags = local.common_tags
}

# ==========================================
# Container Apps Environment
# ==========================================

resource "azurerm_log_analytics_workspace" "container_apps" {
  count               = var.create_container_apps ? 1 : 0
  name                = "log-${local.resource_prefix}-cae"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name
  sku                 = "PerGB2018"
  retention_in_days   = 30

  tags = local.common_tags
}

resource "azurerm_container_app_environment" "main" {
  count                      = var.create_container_apps ? 1 : 0
  name                       = "cae-${local.resource_prefix}"
  location                   = azurerm_resource_group.main.location
  resource_group_name        = azurerm_resource_group.main.name
  log_analytics_workspace_id = azurerm_log_analytics_workspace.container_apps[0].id

  tags = local.common_tags
}

# ==========================================
# User Assigned Managed Identity (for Container Apps)
# ==========================================

resource "azurerm_user_assigned_identity" "container_apps" {
  count               = var.create_container_apps ? 1 : 0
  name                = "id-${local.resource_prefix}-cae"
  location            = azurerm_resource_group.main.location
  resource_group_name = azurerm_resource_group.main.name

  tags = local.common_tags
}

# Key Vault access for Container Apps identity
resource "azurerm_role_assignment" "container_apps_kv" {
  count                = var.create_container_apps ? 1 : 0
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_user_assigned_identity.container_apps[0].principal_id
}

# Storage access for Container Apps identity
resource "azurerm_role_assignment" "container_apps_storage" {
  count                = var.create_container_apps ? 1 : 0
  scope                = azurerm_storage_account.main.id
  role_definition_name = "Storage Table Data Contributor"
  principal_id         = azurerm_user_assigned_identity.container_apps[0].principal_id
}

# Service Bus access for Container Apps identity
resource "azurerm_role_assignment" "container_apps_servicebus" {
  count                = var.create_container_apps && var.create_service_bus ? 1 : 0
  scope                = azurerm_servicebus_namespace.main[0].id
  role_definition_name = "Azure Service Bus Data Owner"
  principal_id         = azurerm_user_assigned_identity.container_apps[0].principal_id
}

# ACR pull permission for Container Apps identity
resource "azurerm_role_assignment" "container_apps_acr" {
  count                = var.create_container_apps ? 1 : 0
  scope                = azurerm_container_registry.main[0].id
  role_definition_name = "AcrPull"
  principal_id         = azurerm_user_assigned_identity.container_apps[0].principal_id
}

# ==========================================
# Container App: API
# ==========================================

resource "azurerm_container_app" "api" {
  count                        = var.create_container_apps ? 1 : 0
  name                         = "ca-${local.resource_prefix}-api"
  container_app_environment_id = azurerm_container_app_environment.main[0].id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_apps[0].id]
  }

  registry {
    server   = azurerm_container_registry.main[0].login_server
    identity = azurerm_user_assigned_identity.container_apps[0].id
  }

  ingress {
    external_enabled = true
    target_port      = 8080
    transport        = "auto"

    traffic_weight {
      percentage      = 100
      latest_revision = true
    }
  }

  template {
    min_replicas = var.api_min_replicas
    max_replicas = var.api_max_replicas

    container {
      name   = "api"
      image  = "${azurerm_container_registry.main[0].login_server}/${var.api_container_image}"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name  = "StorageConnectionString"
        value = azurerm_storage_account.main.primary_connection_string
      }

      env {
        name  = "KeyVaultUrl"
        value = azurerm_key_vault.main.vault_uri
      }

      env {
        name  = "ServiceBusConnectionString"
        value = var.create_service_bus ? azurerm_servicebus_namespace.main[0].default_primary_connection_string : ""
      }

      env {
        name  = "AzureSignalRConnectionString"
        value = var.create_signalr ? azurerm_signalr_service.main[0].primary_connection_string : ""
      }

      env {
        name  = "AzureAd__TenantId"
        value = var.tenant_id
      }

      env {
        name  = "AzureAd__ClientId"
        value = module.app_registration.client_id
      }

      env {
        name  = "AzureAd__Audience"
        value = "api://${module.app_registration.client_id}"
      }

      env {
        name  = "ApplicationInsights__ConnectionString"
        value = azurerm_application_insights.main.connection_string
      }

      liveness_probe {
        path                    = "/health"
        port                    = 8080
        transport               = "HTTP"
        interval_seconds        = 30
        failure_count_threshold = 3
      }

      readiness_probe {
        path                    = "/health/ready"
        port                    = 8080
        transport               = "HTTP"
        interval_seconds        = 10
        failure_count_threshold = 3
      }
    }

    http_scale_rule {
      name                = "http-scaling"
      concurrent_requests = 50
    }
  }

  tags = local.common_tags
}

# ==========================================
# Container App: Worker
# ==========================================

resource "azurerm_container_app" "worker" {
  count                        = var.create_container_apps && var.create_service_bus ? 1 : 0
  name                         = "ca-${local.resource_prefix}-worker"
  container_app_environment_id = azurerm_container_app_environment.main[0].id
  resource_group_name          = azurerm_resource_group.main.name
  revision_mode                = "Single"

  identity {
    type         = "UserAssigned"
    identity_ids = [azurerm_user_assigned_identity.container_apps[0].id]
  }

  registry {
    server   = azurerm_container_registry.main[0].login_server
    identity = azurerm_user_assigned_identity.container_apps[0].id
  }

  template {
    min_replicas = var.worker_min_replicas
    max_replicas = var.worker_max_replicas

    container {
      name   = "worker"
      image  = "${azurerm_container_registry.main[0].login_server}/${var.worker_container_image}"
      cpu    = 0.5
      memory = "1Gi"

      env {
        name  = "DOTNET_ENVIRONMENT"
        value = var.environment == "prod" ? "Production" : "Development"
      }

      env {
        name  = "StorageConnectionString"
        value = azurerm_storage_account.main.primary_connection_string
      }

      env {
        name  = "KeyVaultUrl"
        value = azurerm_key_vault.main.vault_uri
      }

      env {
        name  = "ServiceBusConnectionString"
        value = azurerm_servicebus_namespace.main[0].default_primary_connection_string
      }

      env {
        name  = "AzureSignalRConnectionString"
        value = var.create_signalr ? azurerm_signalr_service.main[0].primary_connection_string : ""
      }

      env {
        name  = "ApplicationInsights__ConnectionString"
        value = azurerm_application_insights.main.connection_string
      }
    }

    # KEDA scale rule based on Service Bus queue
    custom_scale_rule {
      name             = "servicebus-scaling"
      custom_rule_type = "azure-servicebus"
      metadata = {
        queueName      = "drift-analysis-requests"
        messageCount   = "5"
        connectionFromEnv = "ServiceBusConnectionString"
      }
    }
  }

  tags = local.common_tags
}
