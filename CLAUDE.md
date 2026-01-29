# Chivato - Azure Infrastructure Drift Detector

## Descripcion del Proyecto

**Chivato** es un agente de monitoreo de drift de infraestructura Azure. Su funcion principal es detectar discrepancias entre la configuracion definida en pipelines de Azure DevOps (ADO) y el estado real de los recursos en Azure.

## Stack Tecnologico

| Componente | Tecnologia |
|------------|------------|
| Frontend | Vite + React + MSAL |
| Backend | C# .NET 10 Azure Functions |
| Autenticacion | Microsoft Entra ID (Auth Code + PKCE) |
| Storage | Azure Table Storage |
| Secretos | Azure Key Vault |
| Email | Azure Communication Services |
| AI Analysis | Azure AI Foundry (GPT-5) |
| CI/CD Source | Azure DevOps Pipelines |

## Arquitectura de Alto Nivel

```
                        ┌──────────────────┐
                        │  Microsoft       │
                        │  Entra ID        │
                        │  (Auth + Roles)  │
                        └────────┬─────────┘
                                 │ Auth Code + PKCE
                                 ▼
┌─────────────────┐     ┌──────────────────────────────────────────────┐
│   UI (React)    │────▶│           Azure Functions                    │
│  - Login/Logout │     │  ┌─────────────────────────────────────────┐ │
│  - Config Timer │     │  │ TimerTrigger (24h configurable)         │ │
│  - Pipelines    │     │  │   └─▶ ADO Pipeline Scanner              │ │
│  - Credentials  │     │  │   └─▶ Azure Resource Inspector          │ │
│  - Emails       │     │  │   └─▶ AI Drift Analyzer (GPT-5)         │ │
└─────────────────┘     │  │   └─▶ Report Generator                  │ │
                        │  └─────────────────────────────────────────┘ │
                        └──────────────────────────────────────────────┘
                                         │
    ┌────────────────────────────────────┼────────────────────────────────────┐
    ▼                    ▼               ▼               ▼                    ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│ Table       │  │ Key Vault   │  │ AI Foundry  │  │ Comm Svc    │  │ ADO + Azure │
│ Storage     │  │ (Secrets)   │  │ (GPT-5)     │  │ (Email)     │  │ (APIs)      │
└─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘  └─────────────┘
```

## Seguridad

- **Autenticacion**: Entra ID con Authorization Code + PKCE
- **Roles**: Chivato.User (lectura), Chivato.Admin (configuracion)
- **Secretos**: Todos en Azure Key Vault (nunca en Table Storage)
- **Expiracion**: Monitoreo de credenciales con alertas 30/7/0 dias

## Diseño Visual

- **Colores**: Negro / Naranja
- **Temas**: Light (día), Dark (noche), System (auto)
- **Tipografía**: Inter
- **Brand Primary**: `#FF6B00` (naranja)
- **Accent Dark**: `#1A1A1A` (negro)

## Funcionalidades Principales

### 0. Autenticacion y Seguridad
- [ ] Login con Microsoft Entra ID (PKCE)
- [ ] Roles: Chivato.User y Chivato.Admin
- [ ] Gestion de conexiones (Azure, ADO, AI, Email)
- [ ] Secretos en Key Vault
- [ ] Monitoreo de expiracion de credenciales

### 1. Configuracion (UI)
- [ ] Configurar frecuencia del timer (default: 24h)
- [ ] Seleccionar pipelines de ADO a monitorear
- [ ] Configurar lista de subscripciones/resource groups
- [ ] Gestionar lista de emails para reportes
- [ ] Configurar conexiones y credenciales (solo Admin)

### 2. Scanner de Pipelines ADO
- [ ] Conectar con Azure DevOps API
- [ ] Extraer configuracion de recursos de los pipelines YAML
- [ ] Parsear definiciones de infraestructura (ARM/Bicep/Terraform)

### 3. Inspector de Recursos Azure
- [ ] Conectar con Azure Resource Manager API
- [ ] Obtener estado actual de recursos por subscription/RG
- [ ] Comparar con configuracion esperada

### 4. Analizador de Drift (AI)
- [ ] Enviar contextos a Azure AI Foundry (GPT-5)
- [ ] Analizar discrepancias entre estado esperado vs actual
- [ ] Clasificar severidad del drift
- [ ] Generar recomendaciones

### 5. Sistema de Reportes
- [ ] Almacenar drift records en Azure Table
- [ ] Generar reportes HTML/PDF
- [ ] Enviar emails programados

## Estructura del Proyecto

```
chivato/
├── CLAUDE.md                              # Este archivo
├── docs/
│   ├── architecture/
│   │   ├── system-design.md               # Componentes y modelo de datos
│   │   ├── ai-integration.md              # Integracion GPT-5
│   │   └── entra-id-setup.md              # Configuracion autenticacion
│   ├── requirements/
│   │   ├── functional-requirements.md     # RF-00 a RF-14
│   │   └── non-functional-requirements.md # Performance, seguridad, costos
│   └── api/                               # Documentacion de APIs (pendiente)
└── src/
    ├── ui/                                # Vite + React + MSAL
    └── functions/                         # Azure Functions C# .NET 10
```

## Estado Actual

- [x] Estructura de carpetas creada
- [x] Documentacion inicial
- [x] Requisitos funcionales (RF-00 a RF-14)
- [x] Requisitos no funcionales
- [x] Diseño de sistema y modelo de datos
- [x] Diseño de integracion AI
- [x] Diseño de autenticacion Entra ID
- [ ] Crear App Registration en Entra ID
- [ ] Inicializar proyecto Vite React
- [ ] Inicializar proyecto Azure Functions .NET 10
- [ ] Implementacion UI
- [ ] Implementacion Functions
- [ ] Integracion AI
- [ ] Testing
- [ ] Deployment

## Notas de Desarrollo

### Por que GPT-5?
El analisis de drift requiere procesar grandes contextos:
- Definiciones completas de pipelines YAML
- Configuraciones de recursos Azure (ARM/Bicep)
- Estado actual de multiples recursos
GPT-5 ofrece ventana de contexto extendida necesaria para este analisis.

### Azure Table Storage
Eleccion economica para almacenar:
- Configuraciones de la aplicacion
- Historico de drift records
- Metadatos de pipelines monitoreados

## Proximos Pasos

1. ~~Definir requisitos funcionales detallados~~ ✓
2. ~~Diseñar modelo de datos para Azure Table~~ ✓
3. ~~Diseñar autenticacion Entra ID~~ ✓
4. Crear App Registration en Azure Portal
5. Inicializar proyecto Vite React con MSAL
6. Inicializar proyecto Azure Functions .NET 10
7. Implementar gestion de conexiones y Key Vault
8. Implementar integracion con ADO y Azure RM APIs
