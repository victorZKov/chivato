#!/bin/bash

# ==========================================
# Chivato Infrastructure Setup Script
# ==========================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TERRAFORM_DIR="$SCRIPT_DIR/../terraform"
PROJECT_ROOT="$SCRIPT_DIR/../.."

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Functions
log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    if ! command -v az &> /dev/null; then
        log_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi

    if ! command -v terraform &> /dev/null; then
        log_error "Terraform is not installed. Please install it first."
        exit 1
    fi

    # Check if logged in to Azure
    if ! az account show &> /dev/null; then
        log_error "Not logged in to Azure. Please run 'az login' first."
        exit 1
    fi

    log_success "Prerequisites check passed"
}

# Get current user info
get_user_info() {
    log_info "Getting current user information..."

    USER_ID=$(az ad signed-in-user show --query id -o tsv)
    USER_EMAIL=$(az ad signed-in-user show --query userPrincipalName -o tsv)
    TENANT_ID=$(az account show --query tenantId -o tsv)
    SUBSCRIPTION_ID=$(az account show --query id -o tsv)
    SUBSCRIPTION_NAME=$(az account show --query name -o tsv)

    echo ""
    echo "Current Azure Context:"
    echo "  User: $USER_EMAIL"
    echo "  User Object ID: $USER_ID"
    echo "  Tenant ID: $TENANT_ID"
    echo "  Subscription: $SUBSCRIPTION_NAME ($SUBSCRIPTION_ID)"
    echo ""
}

# Create tfvars file
create_tfvars() {
    local ENV=$1
    local TFVARS_FILE="$TERRAFORM_DIR/my-${ENV}.tfvars"

    if [ -f "$TFVARS_FILE" ]; then
        log_warning "File $TFVARS_FILE already exists. Skipping creation."
        return
    fi

    log_info "Creating $TFVARS_FILE..."

    cat > "$TFVARS_FILE" << EOF
# ==========================================
# Chivato - $ENV Environment
# Generated: $(date)
# ==========================================

tenant_id       = "$TENANT_ID"
subscription_id = "$SUBSCRIPTION_ID"
environment     = "$ENV"
location        = "westeurope"

# SPA Redirect URIs
spa_redirect_uris = [
  "http://localhost:5173",
  "http://localhost:5173/"
]

# Role Assignments
admin_user_ids = [
  "$USER_ID"  # $USER_EMAIL
]

user_user_ids = []

# Infrastructure flags
create_function_app          = false
create_communication_service = false
create_openai                = false

# CORS
cors_allowed_origins = [
  "http://localhost:5173"
]
EOF

    log_success "Created $TFVARS_FILE"
}

# Initialize Terraform
init_terraform() {
    log_info "Initializing Terraform..."
    cd "$TERRAFORM_DIR"
    terraform init
    log_success "Terraform initialized"
}

# Plan Terraform
plan_terraform() {
    local ENV=$1
    local TFVARS_FILE="$TERRAFORM_DIR/my-${ENV}.tfvars"

    if [ ! -f "$TFVARS_FILE" ]; then
        log_error "File $TFVARS_FILE not found. Run setup first."
        exit 1
    fi

    log_info "Planning Terraform changes..."
    cd "$TERRAFORM_DIR"
    terraform plan -var-file="$TFVARS_FILE"
}

# Apply Terraform
apply_terraform() {
    local ENV=$1
    local TFVARS_FILE="$TERRAFORM_DIR/my-${ENV}.tfvars"

    if [ ! -f "$TFVARS_FILE" ]; then
        log_error "File $TFVARS_FILE not found. Run setup first."
        exit 1
    fi

    log_info "Applying Terraform changes..."
    cd "$TERRAFORM_DIR"
    terraform apply -var-file="$TFVARS_FILE"
}

# Generate config files
generate_configs() {
    log_info "Generating configuration files..."
    cd "$TERRAFORM_DIR"

    # Frontend .env
    local FRONTEND_ENV="$PROJECT_ROOT/ui/.env"
    terraform output -raw frontend_env_config > "$FRONTEND_ENV"
    log_success "Generated $FRONTEND_ENV"

    # Backend local.settings.json
    local BACKEND_SETTINGS="$PROJECT_ROOT/functions/local.settings.json"
    terraform output -raw backend_local_settings > "$BACKEND_SETTINGS"
    log_success "Generated $BACKEND_SETTINGS"

    echo ""
    log_info "Configuration files generated:"
    echo "  Frontend: $FRONTEND_ENV"
    echo "  Backend: $BACKEND_SETTINGS"
}

# Show outputs
show_outputs() {
    log_info "Terraform outputs:"
    cd "$TERRAFORM_DIR"
    terraform output
}

# Main menu
show_help() {
    echo ""
    echo "Usage: $0 <command> [environment]"
    echo ""
    echo "Commands:"
    echo "  setup <env>     Create tfvars file for environment (dev/prod)"
    echo "  init            Initialize Terraform"
    echo "  plan <env>      Plan Terraform changes"
    echo "  apply <env>     Apply Terraform changes"
    echo "  configs         Generate frontend/backend config files"
    echo "  outputs         Show Terraform outputs"
    echo "  info            Show current Azure context"
    echo ""
    echo "Example:"
    echo "  $0 setup dev    # Create my-dev.tfvars with your user as admin"
    echo "  $0 init         # Initialize Terraform"
    echo "  $0 plan dev     # Preview changes"
    echo "  $0 apply dev    # Apply changes"
    echo "  $0 configs      # Generate .env and local.settings.json"
    echo ""
}

# Main
main() {
    local COMMAND=${1:-help}
    local ENV=${2:-dev}

    case $COMMAND in
        setup)
            check_prerequisites
            get_user_info
            create_tfvars "$ENV"
            ;;
        init)
            check_prerequisites
            init_terraform
            ;;
        plan)
            check_prerequisites
            plan_terraform "$ENV"
            ;;
        apply)
            check_prerequisites
            apply_terraform "$ENV"
            ;;
        configs)
            generate_configs
            ;;
        outputs)
            show_outputs
            ;;
        info)
            check_prerequisites
            get_user_info
            ;;
        help|--help|-h)
            show_help
            ;;
        *)
            log_error "Unknown command: $COMMAND"
            show_help
            exit 1
            ;;
    esac
}

main "$@"
