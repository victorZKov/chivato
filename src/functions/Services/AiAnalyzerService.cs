using Microsoft.Extensions.Logging;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Chivato.Functions.Models;
using System.Text.Json;
using System.ClientModel;

namespace Chivato.Functions.Services;

public class AiAnalyzerService : IAiAnalyzerService
{
    private readonly ILogger<AiAnalyzerService> _logger;
    private const int MaxTokensPerChunk = 8000;
    private const int MaxResponseTokens = 4000;

    public AiAnalyzerService(ILogger<AiAnalyzerService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(string endpoint, string deploymentName, string apiKey)
    {
        try
        {
            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            var chatClient = client.GetChatClient(deploymentName);

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("You are a helpful assistant."),
                new UserChatMessage("Say 'connection test successful' in exactly those words.")
            };

            var response = await chatClient.CompleteChatAsync(messages);

            _logger.LogInformation("AI connection test successful");
            return response.Value.Content.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI connection test failed for endpoint {Endpoint}", endpoint);
            return false;
        }
    }

    public async Task<DriftAnalysisResult> AnalyzeDriftAsync(
        PipelineScanResult pipelineResult,
        IEnumerable<AzureResourceState> currentResources,
        string endpoint,
        string deploymentName,
        string apiKey)
    {
        var result = new DriftAnalysisResult();

        try
        {
            var client = new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey));
            var chatClient = client.GetChatClient(deploymentName);

            // Prepare the context for analysis
            var pipelineContext = PreparePipelineContext(pipelineResult);
            var resourceContext = PrepareResourceContext(currentResources);

            // Chunk the data if necessary
            var chunks = ChunkContent(pipelineContext, resourceContext);

            var allDriftItems = new List<DriftItem>();

            foreach (var chunk in chunks)
            {
                var chunkResult = await AnalyzeChunkAsync(chatClient, chunk, pipelineResult.PipelineName);
                allDriftItems.AddRange(chunkResult);
            }

            // Consolidate results
            result.DriftItems = allDriftItems;
            result.OverallRisk = DetermineOverallRisk(allDriftItems);
            result.ActionRequired = allDriftItems.Any(d =>
                d.Severity == "CRITICAL" || d.Severity == "HIGH");
            result.Summary = GenerateSummary(allDriftItems, pipelineResult.PipelineName);

            _logger.LogInformation(
                "Drift analysis complete. Found {Count} drift items. Risk: {Risk}",
                allDriftItems.Count, result.OverallRisk);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during drift analysis");
            result.Summary = $"Error durante el análisis: {ex.Message}";
            result.OverallRisk = "UNKNOWN";
        }

        return result;
    }

    private string PreparePipelineContext(PipelineScanResult pipeline)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## Pipeline: {pipeline.PipelineName}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(pipeline.YamlContent))
        {
            sb.AppendLine("### YAML Configuration:");
            sb.AppendLine("```yaml");
            sb.AppendLine(pipeline.YamlContent.Length > 5000
                ? pipeline.YamlContent[..5000] + "... (truncated)"
                : pipeline.YamlContent);
            sb.AppendLine("```");
        }

        if (pipeline.InfrastructureDefinitions.Any())
        {
            sb.AppendLine("### Infrastructure as Code Files:");
            foreach (var def in pipeline.InfrastructureDefinitions)
            {
                sb.AppendLine($"- Type: {def.Type}, Path: {def.FilePath}");
                if (!string.IsNullOrEmpty(def.Content))
                {
                    sb.AppendLine("```");
                    sb.AppendLine(def.Content.Length > 3000
                        ? def.Content[..3000] + "... (truncated)"
                        : def.Content);
                    sb.AppendLine("```");
                }
            }
        }

        return sb.ToString();
    }

    private string PrepareResourceContext(IEnumerable<AzureResourceState> resources)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Current Azure Resources:");
        sb.AppendLine();

        foreach (var resource in resources)
        {
            sb.AppendLine($"### {resource.Name} ({resource.ResourceType})");
            sb.AppendLine($"- Location: {resource.Location}");
            sb.AppendLine($"- Resource Group: {resource.ResourceGroup}");

            if (resource.Tags.Any())
            {
                sb.AppendLine("- Tags:");
                foreach (var tag in resource.Tags)
                {
                    sb.AppendLine($"  - {tag.Key}: {tag.Value}");
                }
            }

            if (resource.Properties.Any())
            {
                sb.AppendLine("- Properties:");
                foreach (var prop in resource.Properties.Take(20)) // Limit properties
                {
                    var value = prop.Value?.ToString() ?? "null";
                    if (value.Length > 100) value = value[..100] + "...";
                    sb.AppendLine($"  - {prop.Key}: {value}");
                }
                if (resource.Properties.Count > 20)
                {
                    sb.AppendLine($"  ... and {resource.Properties.Count - 20} more properties");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private List<(string Pipeline, string Resources)> ChunkContent(string pipelineContext, string resourceContext)
    {
        var chunks = new List<(string, string)>();

        // Simple chunking - in production you'd want more sophisticated logic
        if (pipelineContext.Length + resourceContext.Length < MaxTokensPerChunk * 4)
        {
            chunks.Add((pipelineContext, resourceContext));
        }
        else
        {
            // Split resources into chunks
            var resourceLines = resourceContext.Split('\n');
            var currentChunk = new System.Text.StringBuilder();
            var chunkSize = 0;

            foreach (var line in resourceLines)
            {
                if (chunkSize + line.Length > MaxTokensPerChunk * 2)
                {
                    chunks.Add((pipelineContext, currentChunk.ToString()));
                    currentChunk.Clear();
                    chunkSize = 0;
                }
                currentChunk.AppendLine(line);
                chunkSize += line.Length;
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add((pipelineContext, currentChunk.ToString()));
            }
        }

        return chunks;
    }

    private async Task<List<DriftItem>> AnalyzeChunkAsync(
        ChatClient chatClient,
        (string Pipeline, string Resources) chunk,
        string pipelineName)
    {
        var systemPrompt = @"Eres un experto en infraestructura cloud de Azure y DevOps. Tu tarea es analizar la configuración de un pipeline de Azure DevOps y compararla con el estado actual de los recursos en Azure para detectar drift (desviaciones).

Analiza los siguientes aspectos:
1. **Seguridad**: Configuraciones de seguridad incorrectas, secretos expuestos, permisos excesivos
2. **Configuración**: Diferencias entre lo definido en IaC y lo desplegado
3. **Rendimiento**: SKUs o configuraciones que afecten el rendimiento
4. **Costos**: Recursos sobredimensionados o mal configurados
5. **Cumplimiento**: Tags faltantes, naming conventions, políticas

Para cada drift detectado, proporciona:
- ResourceId, ResourceType, ResourceName
- Property: la propiedad afectada
- ExpectedValue: valor esperado según el pipeline/IaC
- ActualValue: valor actual en Azure
- Severity: CRITICAL, HIGH, MEDIUM, LOW, INFO
- Category: security, performance, cost, compliance, configuration
- Description: descripción clara del problema
- Recommendation: acción recomendada

Responde SOLO con un JSON array de objetos drift. Si no hay drift, responde con array vacío [].";

        var userPrompt = $@"Analiza el siguiente pipeline y recursos para detectar drift:

{chunk.Pipeline}

{chunk.Resources}

Responde con el JSON array de drift items detectados:";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = MaxResponseTokens,
            Temperature = 0.3f
        };

        try
        {
            var response = await chatClient.CompleteChatAsync(messages, options);
            var content = response.Value.Content[0].Text;

            // Clean up the response - remove markdown code blocks if present
            content = content.Trim();
            if (content.StartsWith("```json"))
            {
                content = content[7..];
            }
            if (content.StartsWith("```"))
            {
                content = content[3..];
            }
            if (content.EndsWith("```"))
            {
                content = content[..^3];
            }
            content = content.Trim();

            var driftItems = JsonSerializer.Deserialize<List<DriftItem>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return driftItems ?? new List<DriftItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON");
            return new List<DriftItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI service");
            throw;
        }
    }

    private string DetermineOverallRisk(List<DriftItem> items)
    {
        if (!items.Any()) return "NONE";
        if (items.Any(i => i.Severity == "CRITICAL")) return "CRITICAL";
        if (items.Any(i => i.Severity == "HIGH")) return "HIGH";
        if (items.Any(i => i.Severity == "MEDIUM")) return "MEDIUM";
        if (items.Any(i => i.Severity == "LOW")) return "LOW";
        return "INFO";
    }

    private string GenerateSummary(List<DriftItem> items, string pipelineName)
    {
        if (!items.Any())
        {
            return $"No se detectó drift en el pipeline '{pipelineName}'. La infraestructura está alineada con la configuración definida.";
        }

        var criticalCount = items.Count(i => i.Severity == "CRITICAL");
        var highCount = items.Count(i => i.Severity == "HIGH");
        var mediumCount = items.Count(i => i.Severity == "MEDIUM");
        var lowCount = items.Count(i => i.Severity == "LOW");

        var categories = items.GroupBy(i => i.Category)
            .Select(g => $"{g.Key}: {g.Count()}")
            .ToList();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Análisis de drift para pipeline '{pipelineName}':");
        sb.AppendLine($"- Total de desviaciones: {items.Count}");

        if (criticalCount > 0) sb.AppendLine($"- ⛔ Críticas: {criticalCount}");
        if (highCount > 0) sb.AppendLine($"- 🔴 Altas: {highCount}");
        if (mediumCount > 0) sb.AppendLine($"- 🟡 Medias: {mediumCount}");
        if (lowCount > 0) sb.AppendLine($"- 🔵 Bajas: {lowCount}");

        sb.AppendLine($"- Categorías: {string.Join(", ", categories)}");

        if (criticalCount > 0 || highCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("⚠️ Se requiere acción inmediata para las desviaciones críticas y altas.");
        }

        return sb.ToString();
    }
}
