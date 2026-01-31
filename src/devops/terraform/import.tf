# ==========================================
# Import Blocks for Existing Resources
# ==========================================
# Run: terraform plan -generate-config-out=generated.tf
# Or: terraform import <resource> <id>
#
# These import blocks tell Terraform to adopt existing Azure resources
# into its state management.
# ==========================================

# Import existing Resource Group
import {
  to = azurerm_resource_group.main
  id = "/subscriptions/ec654428-81d7-4dcd-8ba8-5b8f632bec29/resourceGroups/rg-chivato-dev"
}

# Import existing Storage Account
import {
  to = azurerm_storage_account.main
  id = "/subscriptions/ec654428-81d7-4dcd-8ba8-5b8f632bec29/resourceGroups/rg-chivato-dev/providers/Microsoft.Storage/storageAccounts/stchivatodev"
}

# Import existing Key Vault
import {
  to = azurerm_key_vault.main
  id = "/subscriptions/ec654428-81d7-4dcd-8ba8-5b8f632bec29/resourceGroups/rg-chivato-dev/providers/Microsoft.KeyVault/vaults/kv-chivato-dev"
}

# Import existing Application Insights
import {
  to = azurerm_application_insights.main
  id = "/subscriptions/ec654428-81d7-4dcd-8ba8-5b8f632bec29/resourceGroups/rg-chivato-dev/providers/Microsoft.Insights/components/appi-chivato-dev"
}
