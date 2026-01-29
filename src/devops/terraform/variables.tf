# ==========================================
# Required Variables
# ==========================================

variable "tenant_id" {
  description = "Azure AD Tenant ID"
  type        = string
}

variable "subscription_id" {
  description = "Azure Subscription ID"
  type        = string
}

variable "environment" {
  description = "Environment name (dev, staging, prod)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "Environment must be one of: dev, staging, prod."
  }
}

variable "location" {
  description = "Azure region for resources"
  type        = string
  default     = "westeurope"
}

# ==========================================
# App Registration Variables
# ==========================================

variable "spa_redirect_uris" {
  description = "Redirect URIs for the SPA application"
  type        = list(string)
  default     = ["http://localhost:5173", "http://localhost:5173/"]
}

variable "admin_user_ids" {
  description = "List of Azure AD User Object IDs to assign Admin role"
  type        = list(string)
  default     = []
}

variable "user_user_ids" {
  description = "List of Azure AD User Object IDs to assign User role"
  type        = list(string)
  default     = []
}

variable "api_permissions" {
  description = "Additional API permissions to request"
  type = list(object({
    api_name    = string
    permissions = list(string)
  }))
  default = []
}

# ==========================================
# Storage Variables
# ==========================================

variable "storage_tables" {
  description = "List of Azure Storage Tables to create"
  type        = list(string)
  default = [
    "Configuration",
    "AzureConnections",
    "AdoConnections",
    "AiConnections",
    "Pipelines",
    "DriftRecords",
    "ScanLogs",
    "AnalysisStatus",
    "EmailRecipients",
    "Plans",
    "Subscriptions",
    "Payments",
    "Invoices",
    "CheckoutSessions",
    "TenantBilling",
    "Counters"
  ]
}

# ==========================================
# Function App Variables
# ==========================================

variable "create_function_app" {
  description = "Whether to create the Function App (set to false for initial setup)"
  type        = bool
  default     = false
}

variable "function_app_sku" {
  description = "SKU for the Function App service plan"
  type        = string
  default     = "Y1" # Consumption plan
}

variable "cors_allowed_origins" {
  description = "CORS allowed origins for the Function App"
  type        = list(string)
  default     = ["http://localhost:5173"]
}

# ==========================================
# Communication Services Variables
# ==========================================

variable "create_communication_service" {
  description = "Whether to create Azure Communication Services"
  type        = bool
  default     = false
}

# ==========================================
# Azure OpenAI Variables
# ==========================================

variable "create_openai" {
  description = "Whether to create Azure OpenAI resources"
  type        = bool
  default     = false
}

variable "openai_location" {
  description = "Azure region for OpenAI (limited availability)"
  type        = string
  default     = "swedencentral"
}

variable "openai_model_name" {
  description = "OpenAI model name to deploy"
  type        = string
  default     = "gpt-4o"
}

variable "openai_model_version" {
  description = "OpenAI model version"
  type        = string
  default     = "2024-05-13"
}

variable "openai_capacity" {
  description = "OpenAI deployment capacity (TPM in thousands)"
  type        = number
  default     = 10
}

# ==========================================
# Container Apps Variables
# ==========================================

variable "create_container_apps" {
  description = "Whether to create Container Apps infrastructure"
  type        = bool
  default     = true
}

variable "api_container_image" {
  description = "Docker image for the API container"
  type        = string
  default     = "chivato-api:latest"
}

variable "worker_container_image" {
  description = "Docker image for the Worker container"
  type        = string
  default     = "chivato-worker:latest"
}

variable "api_min_replicas" {
  description = "Minimum number of API replicas"
  type        = number
  default     = 0
}

variable "api_max_replicas" {
  description = "Maximum number of API replicas"
  type        = number
  default     = 5
}

variable "worker_min_replicas" {
  description = "Minimum number of Worker replicas"
  type        = number
  default     = 0
}

variable "worker_max_replicas" {
  description = "Maximum number of Worker replicas"
  type        = number
  default     = 3
}

# ==========================================
# Service Bus Variables
# ==========================================

variable "create_service_bus" {
  description = "Whether to create Azure Service Bus"
  type        = bool
  default     = true
}

variable "service_bus_sku" {
  description = "SKU for Service Bus namespace"
  type        = string
  default     = "Basic"
}

# ==========================================
# SignalR Variables
# ==========================================

variable "create_signalr" {
  description = "Whether to create Azure SignalR Service"
  type        = bool
  default     = true
}

variable "signalr_sku" {
  description = "SKU for SignalR Service"
  type        = string
  default     = "Free_F1"
}

variable "signalr_capacity" {
  description = "Capacity for SignalR Service (units)"
  type        = number
  default     = 1
}
