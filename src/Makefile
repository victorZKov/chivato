# ==========================================
# Chivato - Development Makefile
# ==========================================
# Usage: make <target>
# ==========================================

.PHONY: help start stop restart logs status build clean init-tables dev

# Default target
help:
	@echo ""
	@echo "Chivato Development Commands"
	@echo "============================"
	@echo ""
	@echo "Docker Compose:"
	@echo "  make start        Start all services (Azurite + API + UI)"
	@echo "  make stop         Stop all services"
	@echo "  make restart      Restart all services"
	@echo "  make logs         View all logs"
	@echo "  make logs-api     View API logs"
	@echo "  make logs-ui      View UI logs"
	@echo "  make status       Show service status"
	@echo "  make build        Rebuild all containers"
	@echo "  make clean        Stop and remove all data"
	@echo ""
	@echo "Local Development:"
	@echo "  make dev-api      Run API locally (requires .NET 10)"
	@echo "  make dev-ui       Run UI locally (requires Node.js)"
	@echo "  make dev          Run both API and UI locally"
	@echo ""
	@echo "Setup:"
	@echo "  make init-tables  Initialize Azurite storage tables"
	@echo "  make setup        Full setup (start + init-tables)"
	@echo ""

# ==========================================
# Docker Compose Commands
# ==========================================

start:
	@echo "Starting Chivato services..."
	docker compose up -d
	@echo ""
	@echo "Services started:"
	@echo "  UI:      http://localhost:5280"
	@echo "  API:     http://localhost:7071/api"
	@echo "  Health:  http://localhost:7071/api/health"
	@echo ""

stop:
	@echo "Stopping Chivato services..."
	docker compose down

restart: stop start

logs:
	docker compose logs -f

logs-api:
	docker compose logs -f api

logs-ui:
	docker compose logs -f ui

logs-azurite:
	docker compose logs -f azurite

status:
	docker compose ps

build:
	@echo "Rebuilding Chivato containers..."
	docker compose build --no-cache

clean:
	@echo "Stopping and removing all data..."
	docker compose down -v
	@echo "Cleanup complete!"

# ==========================================
# Local Development Commands
# ==========================================

dev-api:
	@echo "Starting API locally..."
	cd functions && func start

dev-ui:
	@echo "Starting UI locally..."
	cd ui && npm run dev

dev:
	@echo "Starting both services locally..."
	@echo "Run 'make dev-api' in one terminal"
	@echo "Run 'make dev-ui' in another terminal"

# ==========================================
# Setup Commands
# ==========================================

init-tables:
	@echo "Initializing Azurite tables..."
	@./scripts/docker-dev.sh init-tables

setup: start
	@sleep 5
	@$(MAKE) init-tables
	@echo ""
	@echo "Setup complete! Access the app at http://localhost:5280"

# ==========================================
# Utility Commands
# ==========================================

shell-api:
	docker compose exec api /bin/sh

shell-ui:
	docker compose exec ui /bin/sh

prune:
	docker system prune -f
	docker volume prune -f
