#!/bin/bash

# ==========================================
# Chivato Docker Development Helper
# ==========================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC_DIR="$SCRIPT_DIR/.."

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Check if .env exists, create from example if not
check_env() {
    if [ ! -f "$SRC_DIR/.env" ]; then
        if [ -f "$SRC_DIR/.env.example" ]; then
            log_info "Creating .env from .env.example..."
            cp "$SRC_DIR/.env.example" "$SRC_DIR/.env"
            log_warning "Please edit $SRC_DIR/.env with your Azure credentials"
        fi
    fi
}

# Start all services
start() {
    log_info "Starting Chivato development environment..."
    check_env
    cd "$SRC_DIR"
    docker compose up -d
    log_success "Services started!"
    echo ""
    echo "  UI:      http://localhost:5280"
    echo "  API:     http://localhost:7071/api"
    echo "  Azurite: http://localhost:10002 (Table)"
    echo ""
    echo "Run '$0 logs' to view logs"
}

# Stop all services
stop() {
    log_info "Stopping Chivato development environment..."
    cd "$SRC_DIR"
    docker compose down
    log_success "Services stopped!"
}

# View logs
logs() {
    cd "$SRC_DIR"
    docker compose logs -f "$@"
}

# Rebuild services
rebuild() {
    log_info "Rebuilding Chivato services..."
    cd "$SRC_DIR"
    docker compose build --no-cache
    log_success "Rebuild complete!"
}

# Clean up (remove volumes)
clean() {
    log_warning "This will remove all data including Azurite storage!"
    read -p "Are you sure? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        cd "$SRC_DIR"
        docker compose down -v
        log_success "Cleanup complete!"
    fi
}

# Show status
status() {
    cd "$SRC_DIR"
    docker compose ps
}

# Initialize Azurite tables
init_tables() {
    log_info "Initializing Azurite tables..."

    # Wait for Azurite to be ready
    log_info "Waiting for Azurite..."
    sleep 3

    # Table names from Terraform
    TABLES=(
        "Configuration"
        "AdoConnections"
        "AzureConnections"
        "AiConnections"
        "Pipelines"
        "DriftRecords"
        "EmailRecipients"
        "TenantBilling"
        "Subscriptions"
        "Plans"
        "Payments"
        "Invoices"
        "CheckoutSessions"
        "Counters"
    )

    CONNECTION_STRING="DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;TableEndpoint=http://localhost:10002/devstoreaccount1;"

    for TABLE in "${TABLES[@]}"; do
        log_info "Creating table: $TABLE"
        # Use Azure CLI or curl to create tables
        curl -s -X POST "http://localhost:10002/devstoreaccount1/Tables" \
            -H "Content-Type: application/json" \
            -H "Accept: application/json;odata=nometadata" \
            -H "x-ms-version: 2019-02-02" \
            -d "{\"TableName\":\"$TABLE\"}" || true
    done

    log_success "Tables initialized!"
}

# Show help
show_help() {
    echo ""
    echo "Usage: $0 <command>"
    echo ""
    echo "Commands:"
    echo "  start       Start all services"
    echo "  stop        Stop all services"
    echo "  restart     Restart all services"
    echo "  logs        View logs (add service name for specific service)"
    echo "  status      Show service status"
    echo "  rebuild     Rebuild all containers"
    echo "  clean       Stop and remove all data"
    echo "  init-tables Initialize Azurite tables"
    echo ""
    echo "Examples:"
    echo "  $0 start              # Start all services"
    echo "  $0 logs api           # View API logs only"
    echo "  $0 logs -f ui         # Follow UI logs"
    echo ""
}

# Main
case "${1:-help}" in
    start)
        start
        ;;
    stop)
        stop
        ;;
    restart)
        stop
        start
        ;;
    logs)
        shift
        logs "$@"
        ;;
    status)
        status
        ;;
    rebuild)
        rebuild
        ;;
    clean)
        clean
        ;;
    init-tables)
        init_tables
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        log_error "Unknown command: $1"
        show_help
        exit 1
        ;;
esac
