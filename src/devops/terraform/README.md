# Chivato - Terraform Infrastructure

Este directorio contiene la configuración de Terraform para desplegar toda la infraestructura de Azure necesaria para Chivato.

## Recursos que se crean

### App Registration (Entra ID)
- App Registration con configuración SPA (PKCE)
- App Roles: `Chivato.Admin`, `Chivato.User`
- API Scope: `access_as_user`
- Service Principal
- Asignación de roles a usuarios

### Infraestructura Azure
- Resource Group
- Storage Account con Tables
- Key Vault
- Application Insights
- Function App (opcional)
- Communication Services (opcional)
- Azure OpenAI (opcional)

## Prerequisitos

1. **Azure CLI** instalado y autenticado:
   ```bash
   az login
   az account set --subscription "<subscription-id>"
   ```

2. **Terraform** >= 1.5.0 instalado:
   ```bash
   brew install terraform  # macOS
   ```

3. **Permisos necesarios** en Azure:
   - `Application Administrator` o `Global Administrator` en Entra ID
   - `Contributor` en la subscription de Azure

## Uso

### 1. Obtener tu Object ID (para asignarte como Admin)

```bash
# Tu Object ID
az ad signed-in-user show --query id -o tsv

# Object ID de otro usuario
az ad user show --id "user@domain.com" --query id -o tsv
```

### 2. Configurar variables

Copia el archivo de ejemplo y configura tus valores:

```bash
cd src/devops/terraform

# Para desarrollo
cp environments/dev.tfvars my-dev.tfvars
```

Edita `my-dev.tfvars` con tus valores:

```hcl
tenant_id       = "tu-tenant-id"
subscription_id = "tu-subscription-id"

admin_user_ids = [
  "tu-object-id"
]
```

### 3. Inicializar Terraform

```bash
terraform init
```

### 4. Ver plan de cambios

```bash
terraform plan -var-file="my-dev.tfvars"
```

### 5. Aplicar cambios

```bash
terraform apply -var-file="my-dev.tfvars"
```

### 6. Obtener outputs

```bash
# Ver todos los outputs
terraform output

# Obtener config para frontend .env
terraform output -raw frontend_env_config > ../../ui/.env

# Obtener config para backend (cuidado: contiene secretos)
terraform output -raw backend_local_settings > ../../functions/local.settings.json
```

## Despliegue por Fases

### Fase 1: Solo App Registration y Storage (desarrollo inicial)

```hcl
# my-dev.tfvars
create_function_app          = false
create_communication_service = false
create_openai                = false
```

```bash
terraform apply -var-file="my-dev.tfvars"
```

Esto crea:
- App Registration con roles
- Storage Account con tables
- Key Vault
- Application Insights

### Fase 2: Añadir Function App

```hcl
create_function_app = true
function_app_sku    = "Y1"  # Consumption
```

### Fase 3: Añadir servicios adicionales

```hcl
create_communication_service = true
create_openai                = true
```

## Asignar usuarios a roles

### Desde Terraform (recomendado)

Añade los Object IDs en el tfvars:

```hcl
admin_user_ids = [
  "11111111-1111-1111-1111-111111111111",
  "22222222-2222-2222-2222-222222222222"
]

user_user_ids = [
  "33333333-3333-3333-3333-333333333333"
]
```

```bash
terraform apply -var-file="my-dev.tfvars"
```

### Desde Azure CLI

```bash
# Obtener IDs necesarios
APP_ID=$(terraform output -raw app_registration_client_id)
SP_ID=$(terraform output -raw service_principal_id)
USER_ID="object-id-del-usuario"

# Obtener ID del rol (Admin o User)
ADMIN_ROLE_ID=$(az ad sp show --id $APP_ID --query "appRoles[?value=='Chivato.Admin'].id" -o tsv)
USER_ROLE_ID=$(az ad sp show --id $APP_ID --query "appRoles[?value=='Chivato.User'].id" -o tsv)

# Asignar rol Admin
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$SP_ID/appRoleAssignedTo" \
  --body "{\"principalId\": \"$USER_ID\", \"resourceId\": \"$SP_ID\", \"appRoleId\": \"$ADMIN_ROLE_ID\"}"
```

## Estructura de archivos

```
terraform/
├── main.tf                     # Configuración principal
├── variables.tf                # Definición de variables
├── outputs.tf                  # Outputs
├── modules/
│   └── app-registration/       # Módulo de App Registration
│       ├── main.tf
│       ├── variables.tf
│       └── outputs.tf
└── environments/
    ├── dev.tfvars              # Variables para desarrollo
    └── prod.tfvars             # Variables para producción
```

## Troubleshooting

### Error: "Insufficient privileges"

Necesitas permisos de `Application Administrator` en Entra ID:

```bash
# Verificar tus roles
az role assignment list --assignee $(az ad signed-in-user show --query id -o tsv) --all
```

### Error: "The subscription is not registered to use namespace 'Microsoft.CognitiveServices'"

Registra el provider:

```bash
az provider register --namespace Microsoft.CognitiveServices
```

### Error con OpenAI: "Model not available in this region"

Cambia `openai_location` a una región con disponibilidad:
- `swedencentral`
- `eastus`
- `eastus2`
- `francecentral`

## Limpieza

Para eliminar todos los recursos:

```bash
terraform destroy -var-file="my-dev.tfvars"
```

**Nota**: En producción, Key Vault tiene soft-delete activado, por lo que la eliminación no es inmediata.
