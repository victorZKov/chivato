# ==========================================
# App Registration Outputs
# ==========================================

output "app_registration_client_id" {
  description = "Client ID for the App Registration (use in frontend .env)"
  value       = module.app_registration.client_id
}

output "app_registration_object_id" {
  description = "Object ID of the App Registration"
  value       = module.app_registration.object_id
}

output "service_principal_id" {
  description = "Object ID of the Service Principal"
  value       = module.app_registration.service_principal_id
}

output "api_identifier_uri" {
  description = "API Identifier URI"
  value       = module.app_registration.identifier_uri
}

# ==========================================
# Resource Group Outputs
# ==========================================

output "resource_group_name" {
  description = "Name of the Resource Group"
  value       = azurerm_resource_group.main.name
}

output "resource_group_id" {
  description = "ID of the Resource Group"
  value       = azurerm_resource_group.main.id
}

# ==========================================
# Storage Outputs
# ==========================================

output "storage_account_name" {
  description = "Name of the Storage Account"
  value       = azurerm_storage_account.main.name
}

output "storage_connection_string" {
  description = "Connection string for the Storage Account"
  value       = azurerm_storage_account.main.primary_connection_string
  sensitive   = true
}

# ==========================================
# Key Vault Outputs
# ==========================================

output "key_vault_name" {
  description = "Name of the Key Vault"
  value       = azurerm_key_vault.main.name
}

output "key_vault_uri" {
  description = "URI of the Key Vault"
  value       = azurerm_key_vault.main.vault_uri
}

# ==========================================
# Application Insights Outputs
# ==========================================

output "app_insights_instrumentation_key" {
  description = "Instrumentation Key for Application Insights"
  value       = azurerm_application_insights.main.instrumentation_key
  sensitive   = true
}

output "app_insights_connection_string" {
  description = "Connection String for Application Insights"
  value       = azurerm_application_insights.main.connection_string
  sensitive   = true
}

# ==========================================
# Function App Outputs
# ==========================================

output "function_app_name" {
  description = "Name of the Function App"
  value       = var.create_function_app ? azurerm_linux_function_app.main[0].name : null
}

output "function_app_url" {
  description = "URL of the Function App"
  value       = var.create_function_app ? "https://${azurerm_linux_function_app.main[0].default_hostname}" : null
}

output "function_app_principal_id" {
  description = "Principal ID of the Function App Managed Identity"
  value       = var.create_function_app ? azurerm_linux_function_app.main[0].identity[0].principal_id : null
}

# ==========================================
# Communication Services Outputs
# ==========================================

output "communication_service_name" {
  description = "Name of the Communication Service"
  value       = var.create_communication_service ? azurerm_communication_service.main[0].name : null
}

# ==========================================
# OpenAI Outputs
# ==========================================

output "openai_endpoint" {
  description = "Endpoint for Azure OpenAI"
  value       = var.create_openai ? azurerm_cognitive_account.openai[0].endpoint : null
}

output "openai_deployment_name" {
  description = "Name of the OpenAI deployment"
  value       = var.create_openai ? azurerm_cognitive_deployment.gpt[0].name : null
}

# ==========================================
# Container Registry Outputs
# ==========================================

output "container_registry_name" {
  description = "Name of the Container Registry"
  value       = var.create_container_apps ? azurerm_container_registry.main[0].name : null
}

output "container_registry_login_server" {
  description = "Login server for the Container Registry"
  value       = var.create_container_apps ? azurerm_container_registry.main[0].login_server : null
}

output "container_registry_admin_username" {
  description = "Admin username for the Container Registry"
  value       = var.create_container_apps ? azurerm_container_registry.main[0].admin_username : null
  sensitive   = true
}

output "container_registry_admin_password" {
  description = "Admin password for the Container Registry"
  value       = var.create_container_apps ? azurerm_container_registry.main[0].admin_password : null
  sensitive   = true
}

# ==========================================
# Service Bus Outputs
# ==========================================

output "service_bus_namespace" {
  description = "Name of the Service Bus namespace"
  value       = var.create_service_bus ? azurerm_servicebus_namespace.main[0].name : null
}

output "service_bus_connection_string" {
  description = "Connection string for Service Bus"
  value       = var.create_service_bus ? azurerm_servicebus_namespace.main[0].default_primary_connection_string : null
  sensitive   = true
}

# ==========================================
# SignalR Outputs
# ==========================================

output "signalr_name" {
  description = "Name of the SignalR Service"
  value       = var.create_signalr ? azurerm_signalr_service.main[0].name : null
}

output "signalr_hostname" {
  description = "Hostname of the SignalR Service"
  value       = var.create_signalr ? azurerm_signalr_service.main[0].hostname : null
}

output "signalr_connection_string" {
  description = "Connection string for SignalR"
  value       = var.create_signalr ? azurerm_signalr_service.main[0].primary_connection_string : null
  sensitive   = true
}

# ==========================================
# Container Apps Outputs
# ==========================================

output "container_apps_environment_name" {
  description = "Name of the Container Apps Environment"
  value       = var.create_container_apps ? azurerm_container_app_environment.main[0].name : null
}

output "api_container_app_url" {
  description = "URL of the API Container App"
  value       = var.create_container_apps ? "https://${azurerm_container_app.api[0].ingress[0].fqdn}" : null
}

output "api_container_app_name" {
  description = "Name of the API Container App"
  value       = var.create_container_apps ? azurerm_container_app.api[0].name : null
}

output "worker_container_app_name" {
  description = "Name of the Worker Container App"
  value       = var.create_container_apps && var.create_service_bus ? azurerm_container_app.worker[0].name : null
}

# ==========================================
# Configuration for Frontend (.env)
# ==========================================

output "frontend_env_config" {
  description = "Environment variables for the frontend .env file"
  value       = <<-EOT
    # Chivato Frontend Configuration
    # Generated by Terraform - ${timestamp()}

    VITE_ENTRA_CLIENT_ID=${module.app_registration.client_id}
    VITE_ENTRA_TENANT_ID=${var.tenant_id}
    VITE_API_URL=${var.create_container_apps ? "https://${azurerm_container_app.api[0].ingress[0].fqdn}/api" : (var.create_function_app ? "https://${azurerm_linux_function_app.main[0].default_hostname}/api" : "http://localhost:7071/api")}
    VITE_REDIRECT_URI=${var.spa_redirect_uris[0]}
    VITE_SIGNALR_URL=${var.create_signalr ? "https://${azurerm_signalr_service.main[0].hostname}" : ""}
  EOT
}

# ==========================================
# Configuration for Backend (appsettings.json)
# ==========================================

output "backend_app_settings" {
  description = "Configuration for backend appsettings (API and Worker)"
  sensitive   = true
  value = jsonencode({
    StorageConnectionString       = azurerm_storage_account.main.primary_connection_string
    KeyVaultUrl                   = azurerm_key_vault.main.vault_uri
    ServiceBusConnectionString    = var.create_service_bus ? azurerm_servicebus_namespace.main[0].default_primary_connection_string : ""
    AzureSignalRConnectionString  = var.create_signalr ? azurerm_signalr_service.main[0].primary_connection_string : ""
    AzureAd = {
      TenantId = var.tenant_id
      ClientId = module.app_registration.client_id
      Audience = "api://${module.app_registration.client_id}"
    }
    ApplicationInsights = {
      ConnectionString = azurerm_application_insights.main.connection_string
    }
  })
}

# ==========================================
# Docker Compose Environment Variables
# ==========================================

output "docker_compose_env" {
  description = "Environment variables for docker-compose.yml"
  sensitive   = true
  value       = <<-EOT
    # Chivato Docker Compose Environment
    # Generated by Terraform - ${timestamp()}

    # Storage
    STORAGE_CONNECTION_STRING=${azurerm_storage_account.main.primary_connection_string}

    # Key Vault
    KEY_VAULT_URL=${azurerm_key_vault.main.vault_uri}

    # Service Bus
    SERVICEBUS_CONNECTION_STRING=${var.create_service_bus ? azurerm_servicebus_namespace.main[0].default_primary_connection_string : ""}

    # SignalR
    SIGNALR_CONNECTION_STRING=${var.create_signalr ? azurerm_signalr_service.main[0].primary_connection_string : ""}

    # Azure AD
    AZURE_AD_TENANT_ID=${var.tenant_id}
    AZURE_AD_CLIENT_ID=${module.app_registration.client_id}
    AZURE_AD_AUDIENCE=api://${module.app_registration.client_id}

    # Application Insights
    APPINSIGHTS_CONNECTION_STRING=${azurerm_application_insights.main.connection_string}

    # Container Registry (for CI/CD)
    ACR_LOGIN_SERVER=${var.create_container_apps ? azurerm_container_registry.main[0].login_server : ""}
    ACR_USERNAME=${var.create_container_apps ? azurerm_container_registry.main[0].admin_username : ""}
    ACR_PASSWORD=${var.create_container_apps ? azurerm_container_registry.main[0].admin_password : ""}
  EOT
}
