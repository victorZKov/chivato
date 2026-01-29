# Requisitos No Funcionales - Chivato

## RNF-01: Rendimiento

| Aspecto | Requisito |
|---------|-----------|
| Tiempo de escaneo | < 5 minutos para 10 pipelines |
| Tiempo de analisis AI | < 2 minutos por pipeline |
| Respuesta UI | < 2 segundos para carga inicial |
| API Response | < 500ms para operaciones CRUD |

---

## RNF-02: Escalabilidad

| Aspecto | Requisito |
|---------|-----------|
| Pipelines | Soportar hasta 100 pipelines monitoreados |
| Subscripciones | Soportar hasta 20 subscripciones |
| Recursos | Analizar hasta 1000 recursos por ejecucion |
| Usuarios concurrentes | Hasta 10 usuarios en UI simultaneamente |

---

## RNF-03: Disponibilidad

| Aspecto | Requisito |
|---------|-----------|
| Uptime Functions | 99.9% (SLA Azure Functions Premium) |
| Tolerancia a fallos | Reintentos automaticos en errores transitorios |
| Recuperacion | < 5 minutos para recuperarse de fallo |

---

## RNF-04: Seguridad

| Aspecto | Requisito |
|---------|-----------|
| Autenticacion | Azure AD / Entra ID |
| Autorizacion | RBAC basado en roles |
| Secretos | Azure Key Vault para credenciales |
| Comunicacion | HTTPS/TLS 1.3 |
| Datos en reposo | Encriptacion con claves gestionadas |

---

## RNF-05: Costos

| Servicio | Estimacion Mensual |
|----------|-------------------|
| Azure Functions | Consumption plan, ~$5-20 |
| Azure Table Storage | ~$1-5 |
| Azure AI Foundry (GPT-5) | ~$50-200 (variable por uso) |
| Azure Communication Services | ~$1-10 |
| **Total Estimado** | **~$60-240/mes** |

---

## RNF-06: Mantenibilidad

| Aspecto | Requisito |
|---------|-----------|
| Logging | Application Insights integrado |
| Monitoreo | Alertas en Azure Monitor |
| Documentacion | Codigo documentado, APIs con OpenAPI |
| Testing | >80% code coverage |

---

## RNF-07: Compatibilidad

| Aspecto | Requisito |
|---------|-----------|
| Browsers | Chrome, Edge, Firefox (ultimas 2 versiones) |
| .NET Runtime | .NET 10 |
| Azure DevOps | API version 7.0+ |
| Azure RM | API version 2023-07-01+ |
