output "application_id" {
  description = "The Application (Object) ID of the App Registration"
  value       = azuread_application.chivato.id
}

output "client_id" {
  description = "The Application (Client) ID of the App Registration"
  value       = azuread_application.chivato.client_id
}

output "object_id" {
  description = "The Object ID of the App Registration"
  value       = azuread_application.chivato.object_id
}

output "service_principal_id" {
  description = "The Object ID of the Service Principal"
  value       = azuread_service_principal.chivato.object_id
}

output "api_scope_id" {
  description = "The ID of the access_as_user API scope"
  value       = random_uuid.api_scope_id.result
}

output "admin_role_id" {
  description = "The ID of the Chivato.Admin role"
  value       = random_uuid.admin_role_id.result
}

output "user_role_id" {
  description = "The ID of the Chivato.User role"
  value       = random_uuid.user_role_id.result
}

output "identifier_uri" {
  description = "The identifier URI for the API"
  value       = "api://${azuread_application.chivato.client_id}"
}
