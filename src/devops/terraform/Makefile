# ==========================================
# Chivato Terraform Makefile
# ==========================================

ENV ?= dev
TFVARS = environments/$(ENV).tfvars

.PHONY: help init plan apply destroy import fmt validate

help: ## Show this help
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "\033[36m%-15s\033[0m %s\n", $$1, $$2}'

init: ## Initialize Terraform (downloads providers, configures backend)
	terraform init -reconfigure

plan: ## Show what changes Terraform will make
	terraform plan -var-file=$(TFVARS)

apply: ## Apply Terraform changes
	terraform apply -var-file=$(TFVARS)

apply-auto: ## Apply Terraform changes without confirmation
	terraform apply -var-file=$(TFVARS) -auto-approve

destroy: ## Destroy all Terraform-managed resources (DANGER!)
	terraform destroy -var-file=$(TFVARS)

import: ## Import existing resources (run after init)
	terraform plan -var-file=$(TFVARS)

fmt: ## Format Terraform files
	terraform fmt -recursive

validate: ## Validate Terraform configuration
	terraform validate

output: ## Show Terraform outputs
	terraform output

state-list: ## List resources in Terraform state
	terraform state list

# ==========================================
# Dev environment shortcuts
# ==========================================

dev-init: ## Initialize for dev environment
	@$(MAKE) init ENV=dev

dev-plan: ## Plan for dev environment
	@$(MAKE) plan ENV=dev

dev-apply: ## Apply for dev environment
	@$(MAKE) apply ENV=dev

# ==========================================
# First-time setup
# ==========================================

setup: ## First-time setup: init and import existing resources
	@echo "Initializing Terraform..."
	@$(MAKE) init
	@echo ""
	@echo "Planning to import existing resources..."
	@$(MAKE) plan ENV=$(ENV)
	@echo ""
	@echo "Review the plan above. If it looks correct, run: make apply ENV=$(ENV)"
