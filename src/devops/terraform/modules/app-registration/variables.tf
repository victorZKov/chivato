variable "app_name" {
  description = "Display name for the App Registration"
  type        = string
}

variable "environment" {
  description = "Environment name"
  type        = string
}

variable "tenant_id" {
  description = "Azure AD Tenant ID"
  type        = string
}

variable "spa_redirect_uris" {
  description = "Redirect URIs for the SPA application"
  type        = list(string)
  default     = ["http://localhost:5173"]
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
