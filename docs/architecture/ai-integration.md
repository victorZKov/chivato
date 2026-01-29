# Integracion con Azure AI Foundry - Chivato

## Por que GPT-5?

El analisis de drift de infraestructura requiere procesar y comparar contextos extensos:

1. **Pipeline YAML completo**: Puede tener cientos de lineas incluyendo templates
2. **Definiciones IaC**: ARM templates, Bicep files o Terraform configs
3. **Estado actual de recursos**: JSON con propiedades detalladas de cada recurso
4. **Contexto historico**: Drift anterior para detectar patrones

GPT-5 ofrece:
- Ventana de contexto extendida (ideal para IaC completo)
- Mejor razonamiento sobre configuraciones complejas
- Capacidad de comparacion estructurada

## Estructura del Prompt

### System Prompt

```
Eres un experto en infraestructura Azure y DevOps. Tu tarea es analizar
discrepancias (drift) entre la configuracion definida en pipelines de
Azure DevOps y el estado actual de los recursos en Azure.

Para cada analisis debes:
1. Comparar la configuracion esperada vs el estado actual
2. Identificar diferencias significativas
3. Clasificar la severidad del drift
4. Proporcionar recomendaciones accionables

Severidades:
- CRITICAL: Riesgo de seguridad o disponibilidad inmediato
- HIGH: Configuracion incorrecta que afecta funcionamiento
- MEDIUM: Desviacion de best practices
- LOW: Diferencias cosmeticas o de metadata
- INFO: Cambios esperados o sin impacto

Responde SIEMPRE en formato JSON estructurado.
```

### User Prompt Template

```
Analiza el drift entre la configuracion del pipeline y el estado actual:

## Pipeline: {pipeline_name}
## Subscription: {subscription_id}
## Resource Group: {resource_group}

### CONFIGURACION ESPERADA (del pipeline):
```yaml
{pipeline_iac_content}
```

### ESTADO ACTUAL (de Azure):
```json
{current_resources_json}
```

### ANALISIS PREVIO (ultimas 24h):
{previous_drift_summary}

Proporciona el analisis en el siguiente formato JSON:
{
  "summary": "Resumen ejecutivo del drift",
  "driftItems": [
    {
      "resourceId": "ID del recurso",
      "resourceType": "Tipo de recurso",
      "property": "Propiedad afectada",
      "expectedValue": "Valor esperado",
      "actualValue": "Valor actual",
      "severity": "CRITICAL|HIGH|MEDIUM|LOW|INFO",
      "description": "Descripcion del drift",
      "recommendation": "Accion recomendada",
      "category": "security|performance|cost|compliance|configuration"
    }
  ],
  "overallRisk": "CRITICAL|HIGH|MEDIUM|LOW|NONE",
  "actionRequired": true|false
}
```

## Response Schema

```csharp
public class DriftAnalysisResult
{
    public string Summary { get; set; }
    public List<DriftItem> DriftItems { get; set; }
    public string OverallRisk { get; set; }
    public bool ActionRequired { get; set; }
}

public class DriftItem
{
    public string ResourceId { get; set; }
    public string ResourceType { get; set; }
    public string Property { get; set; }
    public string ExpectedValue { get; set; }
    public string ActualValue { get; set; }
    public string Severity { get; set; }
    public string Description { get; set; }
    public string Recommendation { get; set; }
    public string Category { get; set; }
}
```

## Configuracion del Cliente AI

```csharp
// Usando Azure.AI.OpenAI SDK
var endpoint = new Uri(config["AzureAI:Endpoint"]);
var credential = new DefaultAzureCredential();

var client = new AzureOpenAIClient(endpoint, credential);

var chatClient = client.GetChatClient("gpt-5"); // deployment name

var options = new ChatCompletionOptions
{
    Temperature = 0.1f, // Bajo para respuestas consistentes
    MaxOutputTokenCount = 4000,
    ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
        "drift_analysis",
        BinaryData.FromString(jsonSchema)
    )
};
```

## Manejo de Contexto Grande

Para pipelines con muchos recursos, dividimos el analisis:

```
┌──────────────────────────────────────────────────────────────┐
│                  CONTEXT CHUNKING STRATEGY                    │
└──────────────────────────────────────────────────────────────┘

Pipeline con 50 recursos
         │
         ▼
┌─────────────────────┐
│ Agrupar por tipo    │
│ de recurso          │
└─────────┬───────────┘
          │
          ├──► Chunk 1: App Services (10 recursos)
          │         │
          │         ▼
          │    ┌─────────────┐
          │    │ AI Analysis │
          │    └─────────────┘
          │
          ├──► Chunk 2: Storage Accounts (8 recursos)
          │         │
          │         ▼
          │    ┌─────────────┐
          │    │ AI Analysis │
          │    └─────────────┘
          │
          ├──► Chunk 3: SQL Databases (5 recursos)
          │         │
          │         ▼
          │    ┌─────────────┐
          │    │ AI Analysis │
          │    └─────────────┘
          │
          └──► ... mas chunks
                    │
                    ▼
          ┌─────────────────────┐
          │ Merge & Deduplicate │
          │ Results             │
          └─────────────────────┘
```

## Estimacion de Costos AI

| Escenario | Tokens Input | Tokens Output | Costo Estimado |
|-----------|--------------|---------------|----------------|
| 1 pipeline, 10 recursos | ~8,000 | ~1,500 | ~$0.10 |
| 10 pipelines, 100 recursos | ~80,000 | ~15,000 | ~$1.00 |
| 50 pipelines, 500 recursos | ~400,000 | ~75,000 | ~$5.00 |
| Ejecucion diaria (30 dias) | - | - | ~$30-150 |

*Costos aproximados basados en pricing de Azure OpenAI para modelos avanzados*

## Retry y Error Handling

```csharp
// Polly retry policy para llamadas a AI
var retryPolicy = Policy
    .Handle<RequestFailedException>()
    .Or<HttpRequestException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt =>
            TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            _logger.LogWarning(
                "Retry {RetryCount} for AI call after {Delay}s due to {Exception}",
                retryCount, timeSpan.TotalSeconds, exception.Message);
        });
```
