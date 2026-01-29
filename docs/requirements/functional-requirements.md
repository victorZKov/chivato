# Requisitos Funcionales - Chivato

## RF-00: Autenticacion de Usuarios (Entra ID)

| ID | RF-00 |
|----|-------|
| Descripcion | El sistema debe autenticar usuarios mediante Microsoft Entra ID |
| Prioridad | Critica |
| Actor | Usuario/Administrador |

### Criterios de Aceptacion

- [ ] Implementar flujo Authorization Code + PKCE (para SPA)
- [ ] Requiere App Registration en Entra ID
- [ ] Los usuarios deben tener el rol "Chivato.User" asignado para acceder
- [ ] Los administradores deben tener el rol "Chivato.Admin" para configurar
- [ ] Redirigir a login de Microsoft si no esta autenticado
- [ ] Almacenar tokens en memoria (no localStorage por seguridad)
- [ ] Implementar refresh token silencioso
- [ ] Mostrar informacion del usuario logueado en UI
- [ ] Boton de logout que invalida sesion

### Configuracion de App Registration

```
App Registration: "Chivato"
├── Authentication
│   ├── Platform: Single-page application (SPA)
│   ├── Redirect URIs:
│   │   ├── https://chivato.azurewebsites.net/
│   │   └── http://localhost:5173/ (dev)
│   └── Enable: Authorization code flow with PKCE
│
├── API Permissions
│   ├── Microsoft Graph: User.Read (delegated)
│   └── (opcional) Azure Service Management: user_impersonation
│
├── App Roles
│   ├── Chivato.User (usuarios normales - solo lectura)
│   └── Chivato.Admin (administradores - configuracion)
│
└── Expose an API (opcional, si backend valida tokens)
    └── Scope: api://{client-id}/access_as_user
```

### Roles y Permisos

| Rol | Permisos |
|-----|----------|
| Chivato.User | Ver dashboard, ver drift records, ver configuracion (read-only) |
| Chivato.Admin | Todo lo anterior + configurar conexiones, pipelines, emails, timer |

---

## RF-00b: Monitoreo de Expiracion de Credenciales

| ID | RF-00b |
|----|--------|
| Descripcion | El sistema debe monitorear y alertar sobre credenciales proximas a expirar |
| Prioridad | Alta |
| Actor | Sistema |

### Criterios de Aceptacion

- [ ] Verificar fecha de expiracion de cada credencial almacenada
- [ ] Alertar 30 dias antes de expiracion (configurable)
- [ ] Alertar 7 dias antes de expiracion (urgente)
- [ ] Alertar el dia de expiracion (critico)
- [ ] Mostrar estado de credenciales en dashboard:
  - Verde: > 30 dias para expirar
  - Amarillo: 7-30 dias para expirar
  - Rojo: < 7 dias o expirada
- [ ] Enviar email de alerta a administradores cuando credencial esta por expirar
- [ ] Bloquear uso de credenciales expiradas con mensaje claro
- [ ] Log de intentos de uso de credenciales expiradas

### Credenciales a Monitorear

| Tipo | Campo de Expiracion |
|------|---------------------|
| Azure Service Principal | ClientSecret expiry (configurado al crear) |
| ADO PAT | Expiration date del token |
| Azure AI API Key | Generalmente no expiran, pero pueden revocarse |
| ACS Connection String | Generalmente no expiran |

---

## RF-01: Configuracion de Timer

| ID | RF-01 |
|----|-------|
| Descripcion | El sistema debe permitir configurar la frecuencia de ejecucion del analisis de drift |
| Prioridad | Alta |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] El usuario puede configurar el intervalo en horas (minimo 1h, maximo 168h/1 semana)
- [ ] Valor por defecto: 24 horas
- [ ] La configuracion se persiste en Azure Table Storage
- [ ] Cambios en la configuracion se aplican en el proximo ciclo

---

## RF-02: Gestion de Pipelines ADO

| ID | RF-02 |
|----|-------|
| Descripcion | El sistema debe permitir seleccionar y gestionar los pipelines de ADO a monitorear |
| Prioridad | Alta |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] Conexion con Azure DevOps mediante PAT o Service Principal
- [ ] Listar organizaciones/proyectos disponibles
- [ ] Seleccionar pipelines especificos para monitorear
- [ ] Activar/desactivar monitoreo por pipeline
- [ ] Almacenar configuracion de pipelines seleccionados

---

## RF-03: Configuracion de Subscripciones/Resource Groups

| ID | RF-03 |
|----|-------|
| Descripcion | El sistema debe permitir configurar que subscripciones y resource groups analizar |
| Prioridad | Alta |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] Listar subscripciones accesibles con las credenciales configuradas
- [ ] Seleccionar subscripciones a monitorear
- [ ] Filtrar por resource groups dentro de cada subscription
- [ ] Mapear pipelines a subscriptions/RGs correspondientes

---

## RF-04: Gestion de Destinatarios de Reportes

| ID | RF-04 |
|----|-------|
| Descripcion | El sistema debe permitir configurar la lista de emails que recibiran los reportes |
| Prioridad | Media |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] Agregar/eliminar direcciones de email
- [ ] Validar formato de email
- [ ] Configurar frecuencia de envio (cada ejecucion, solo si hay drift, resumen semanal)
- [ ] Permitir multiples grupos de destinatarios

---

## RF-05: Escaneo de Pipelines

| ID | RF-05 |
|----|-------|
| Descripcion | El sistema debe extraer la configuracion de infraestructura de los pipelines ADO |
| Prioridad | Alta |
| Actor | Sistema |

### Criterios de Aceptacion
- [ ] Leer archivos YAML de pipeline
- [ ] Detectar y parsear templates de ARM
- [ ] Detectar y parsear archivos Bicep
- [ ] Detectar y parsear configuraciones Terraform
- [ ] Extraer recursos definidos y sus propiedades esperadas

---

## RF-06: Inspeccion de Recursos Azure

| ID | RF-06 |
|----|-------|
| Descripcion | El sistema debe obtener el estado actual de los recursos en Azure |
| Prioridad | Alta |
| Actor | Sistema |

### Criterios de Aceptacion
- [ ] Conectar con Azure Resource Manager API
- [ ] Listar recursos por subscription/resource group
- [ ] Obtener propiedades actuales de cada recurso
- [ ] Manejar paginacion de resultados
- [ ] Respetar rate limits de la API

---

## RF-07: Analisis de Drift con AI

| ID | RF-07 |
|----|-------|
| Descripcion | El sistema debe usar AI para analizar discrepancias entre configuracion esperada y estado actual |
| Prioridad | Alta |
| Actor | Sistema |

### Criterios de Aceptacion
- [ ] Enviar contexto a Azure AI Foundry (GPT-5)
- [ ] Identificar recursos con drift
- [ ] Clasificar severidad: Critico, Alto, Medio, Bajo, Info
- [ ] Generar descripcion del drift detectado
- [ ] Proporcionar recomendaciones de correccion

---

## RF-08: Almacenamiento de Drift Records

| ID | RF-08 |
|----|-------|
| Descripcion | El sistema debe almacenar el historico de drift detectado |
| Prioridad | Alta |
| Actor | Sistema |

### Criterios de Aceptacion
- [ ] Crear registro por cada drift detectado
- [ ] Almacenar: timestamp, pipeline, resource, tipo de drift, severidad, descripcion
- [ ] Permitir consulta historica
- [ ] Implementar retencion configurable (default: 90 dias)

---

## RF-09: Generacion y Envio de Reportes

| ID | RF-09 |
|----|-------|
| Descripcion | El sistema debe generar y enviar reportes de drift por email |
| Prioridad | Media |
| Actor | Sistema |

### Criterios de Aceptacion
- [ ] Generar reporte HTML con resumen de drift
- [ ] Incluir graficas de tendencia
- [ ] Agrupar por severidad
- [ ] Enviar via Azure Communication Services
- [ ] Incluir enlace a UI para detalles

---

## RF-10: Dashboard de Visualizacion

| ID | RF-10 |
|----|-------|
| Descripcion | La UI debe mostrar el estado actual y historico del drift |
| Prioridad | Media |
| Actor | Usuario |

### Criterios de Aceptacion
- [ ] Vista de resumen con metricas principales
- [ ] Lista de drift activo agrupado por severidad
- [ ] Historico de ejecuciones
- [ ] Filtros por fecha, pipeline, subscription, severidad
- [ ] Detalle de cada drift detectado

---

## RF-11: Configuracion de Conexion a Azure

| ID | RF-11 |
|----|-------|
| Descripcion | El sistema debe permitir configurar las credenciales para acceder a Azure Resource Manager |
| Prioridad | Alta |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] Soportar autenticacion mediante Service Principal (ClientId + ClientSecret + TenantId)
- [ ] Soportar Managed Identity cuando se ejecuta en Azure
- [ ] Validar conexion antes de guardar (test connection)
- [ ] Almacenar secretos en Azure Key Vault (no en Table Storage)
- [ ] Permitir multiples conexiones para diferentes tenants/subscriptions
- [ ] Mostrar estado de conexion (activa, error, expirando)
- [ ] UI para rotar/actualizar credenciales

### Notas de Seguridad
- ClientSecret NUNCA se almacena en Table Storage
- Solo se guarda referencia al secreto en Key Vault
- Los secretos tienen fecha de expiracion configurable

---

## RF-12: Configuracion de Conexion a Azure DevOps

| ID | RF-12 |
|----|-------|
| Descripcion | El sistema debe permitir configurar las credenciales para acceder a Azure DevOps |
| Prioridad | Alta |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] Soportar Personal Access Token (PAT)
- [ ] Soportar Service Principal con OAuth
- [ ] Configurar Organization URL (https://dev.azure.com/{org})
- [ ] Validar conexion y permisos antes de guardar
- [ ] Almacenar PAT/secretos en Azure Key Vault
- [ ] Mostrar permisos disponibles del token (lectura pipelines, repos, etc.)
- [ ] Alertar cuando el PAT este proximo a expirar
- [ ] UI para rotar/actualizar credenciales

### Permisos Minimos Requeridos en ADO
- **Code (Read)**: Para leer archivos YAML de pipelines
- **Build (Read)**: Para listar y leer definiciones de pipelines
- **Project and Team (Read)**: Para listar proyectos

---

## RF-13: Configuracion de Azure AI Foundry

| ID | RF-13 |
|----|-------|
| Descripcion | El sistema debe permitir configurar la conexion a Azure AI Foundry para el analisis con GPT-5 |
| Prioridad | Alta |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] Configurar endpoint de Azure OpenAI / AI Foundry
- [ ] Configurar deployment name del modelo (gpt-5)
- [ ] Soportar autenticacion con API Key o Managed Identity
- [ ] Almacenar API Key en Key Vault si se usa
- [ ] Validar conexion y modelo disponible
- [ ] Configurar parametros del modelo (temperature, max tokens)
- [ ] Mostrar estimacion de costos basado en uso

---

## RF-14: Configuracion de Azure Communication Services

| ID | RF-14 |
|----|-------|
| Descripcion | El sistema debe permitir configurar el servicio de envio de emails |
| Prioridad | Media |
| Actor | Administrador |

### Criterios de Aceptacion
- [ ] Configurar connection string de Azure Communication Services
- [ ] Configurar email "From" (dominio verificado)
- [ ] Almacenar connection string en Key Vault
- [ ] Validar configuracion enviando email de prueba
- [ ] Configurar plantilla HTML del reporte (opcional)
