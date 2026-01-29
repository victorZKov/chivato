terraform {
  required_providers {
    azuread = {
      source  = "hashicorp/azuread"
      version = "~> 2.47"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }
}

# Data sources
data "azuread_client_config" "current" {}

# Microsoft Graph API (for User.Read permission)
data "azuread_application_published_app_ids" "well_known" {}

data "azuread_service_principal" "msgraph" {
  client_id = data.azuread_application_published_app_ids.well_known.result["MicrosoftGraph"]
}

# Random UUID for App Roles
resource "random_uuid" "admin_role_id" {}
resource "random_uuid" "user_role_id" {}
resource "random_uuid" "api_scope_id" {}

# ==========================================
# App Registration
# ==========================================

resource "azuread_application" "chivato" {
  display_name     = var.app_name
  sign_in_audience = "AzureADMyOrg"

  # SPA Configuration (for PKCE flow)
  single_page_application {
    redirect_uris = var.spa_redirect_uris
  }

  # Web Configuration (for API)
  web {
    implicit_grant {
      access_token_issuance_enabled = false
      id_token_issuance_enabled     = false
    }
  }

  # API Configuration
  api {
    mapped_claims_enabled          = false
    requested_access_token_version = 2

    # API Scope for access_as_user
    oauth2_permission_scope {
      admin_consent_description  = "Allow the application to access Chivato API on behalf of the signed-in user."
      admin_consent_display_name = "Access Chivato API"
      enabled                    = true
      id                         = random_uuid.api_scope_id.result
      type                       = "User"
      user_consent_description   = "Allow the application to access Chivato API on your behalf."
      user_consent_display_name  = "Access Chivato API"
      value                      = "access_as_user"
    }
  }

  # App Roles
  app_role {
    allowed_member_types = ["User"]
    description          = "Administrators can manage all aspects of Chivato including configuration and billing."
    display_name         = "Chivato Administrator"
    enabled              = true
    id                   = random_uuid.admin_role_id.result
    value                = "Chivato.Admin"
  }

  app_role {
    allowed_member_types = ["User"]
    description          = "Users can view drift reports and dashboard but cannot modify configuration."
    display_name         = "Chivato User"
    enabled              = true
    id                   = random_uuid.user_role_id.result
    value                = "Chivato.User"
  }

  # Required Resource Access (Microsoft Graph - User.Read)
  required_resource_access {
    resource_app_id = data.azuread_service_principal.msgraph.client_id

    resource_access {
      id   = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["User.Read"]
      type = "Scope"
    }

    resource_access {
      id   = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["openid"]
      type = "Scope"
    }

    resource_access {
      id   = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["profile"]
      type = "Scope"
    }

    resource_access {
      id   = data.azuread_service_principal.msgraph.oauth2_permission_scope_ids["email"]
      type = "Scope"
    }
  }

  # Optional claims for token
  optional_claims {
    access_token {
      name = "email"
    }
    access_token {
      name = "preferred_username"
    }
    id_token {
      name = "email"
    }
    id_token {
      name = "preferred_username"
    }
  }

  # Prevent accidental deletion
  lifecycle {
    prevent_destroy = false # Set to true in production
  }

  tags = ["Chivato", var.environment, "Terraform"]
}

# ==========================================
# Application Identifier URI
# ==========================================
# Set the identifier URI after the application is created
# using the application's client_id

resource "azuread_application_identifier_uri" "chivato" {
  application_id = azuread_application.chivato.id
  identifier_uri = "api://${azuread_application.chivato.client_id}"
}

# ==========================================
# Service Principal
# ==========================================

resource "azuread_service_principal" "chivato" {
  client_id                    = azuread_application.chivato.client_id
  app_role_assignment_required = true # Users must be assigned a role to access

  feature_tags {
    enterprise = true
  }

  depends_on = [azuread_application_identifier_uri.chivato]
}

# ==========================================
# Role Assignments - Admins
# ==========================================

resource "azuread_app_role_assignment" "admins" {
  for_each            = toset(var.admin_user_ids)
  app_role_id         = random_uuid.admin_role_id.result
  principal_object_id = each.value
  resource_object_id  = azuread_service_principal.chivato.object_id
}

# ==========================================
# Role Assignments - Users
# ==========================================

resource "azuread_app_role_assignment" "users" {
  for_each            = toset(var.user_user_ids)
  app_role_id         = random_uuid.user_role_id.result
  principal_object_id = each.value
  resource_object_id  = azuread_service_principal.chivato.object_id
}

# ==========================================
# Pre-authorize the SPA to call the API
# ==========================================

resource "azuread_application_pre_authorized" "spa" {
  application_id       = azuread_application.chivato.id
  authorized_client_id = azuread_application.chivato.client_id

  permission_ids = [
    random_uuid.api_scope_id.result,
  ]
}

# ==========================================
# Admin Consent (grant permissions)
# ==========================================

resource "azuread_service_principal_delegated_permission_grant" "msgraph" {
  service_principal_object_id          = azuread_service_principal.chivato.object_id
  resource_service_principal_object_id = data.azuread_service_principal.msgraph.object_id
  claim_values                         = ["User.Read", "openid", "profile", "email"]
}
