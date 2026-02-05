using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Chivato.Shared.Models;
using System.Text.Json;
using System.ClientModel;

namespace Chivato.Shared.Services;

/// <summary>
/// AI-powered analyzer for Terraform plan output
/// </summary>
public interface ITerraformPlanAnalyzer
{
    /// <summary>
    /// Analyze terraform plan output and extract drift information with AI explanations
    /// </summary>
    Task<TerraformPlanAnalysisResult> AnalyzePlanOutputAsync(
        string terraformPlanOutput,
        string pipelineName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from AI analysis of Terraform plan
/// </summary>
public class TerraformPlanAnalysisResult
{
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string OverallRisk { get; set; } = "NONE";
    public bool ActionRequired { get; set; }
    public List<DriftItem> DriftItems { get; set; } = new();
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Terraform plan summary (e.g., "3 to add, 2 to change, 1 to destroy")
    /// </summary>
    public string? PlanSummary { get; set; }
}

/// <summary>
/// Implementation using Azure OpenAI
/// </summary>
public class TerraformPlanAnalyzer : ITerraformPlanAnalyzer
{
    private readonly string _endpoint;
    private readonly string _deploymentName;
    private readonly string _apiKey;
    private const int MaxResponseTokens = 4000;

    public TerraformPlanAnalyzer(string endpoint, string deploymentName, string apiKey)
    {
        _endpoint = endpoint;
        _deploymentName = deploymentName;
        _apiKey = apiKey;
    }

    public async Task<TerraformPlanAnalysisResult> AnalyzePlanOutputAsync(
        string terraformPlanOutput,
        string pipelineName,
        CancellationToken cancellationToken = default)
    {
        var result = new TerraformPlanAnalysisResult();

        if (string.IsNullOrEmpty(terraformPlanOutput))
        {
            result.Success = false;
            result.ErrorMessage = "No terraform plan output provided";
            return result;
        }

        // Check for terraform errors (authentication, initialization, etc.)
        var hasError = terraformPlanOutput.Contains("Error:", StringComparison.OrdinalIgnoreCase) ||
                       terraformPlanOutput.Contains("Error ", StringComparison.OrdinalIgnoreCase) ||
                       terraformPlanOutput.Contains("failed to", StringComparison.OrdinalIgnoreCase) ||
                       terraformPlanOutput.Contains("│ Error", StringComparison.OrdinalIgnoreCase);
        
        if (hasError)
        {
            // Analyze the error with AI
            try
            {
                var client = new AzureOpenAIClient(new Uri(_endpoint), new ApiKeyCredential(_apiKey));
                var chatClient = client.GetChatClient(_deploymentName);
                
                var errorAnalysis = await AnalyzeErrorWithAIAsync(chatClient, terraformPlanOutput, pipelineName, cancellationToken);
                
                result.Success = false;
                result.ErrorMessage = errorAnalysis.errorMessage;
                result.Summary = errorAnalysis.summary;
                result.OverallRisk = errorAnalysis.severity;
                result.ActionRequired = true;
                
                // Create a drift item to represent the error
                if (!string.IsNullOrEmpty(errorAnalysis.errorMessage))
                {
                    result.DriftItems.Add(new DriftItem
                    {
                        ResourceId = "terraform-execution",
                        ResourceType = "terraform_error",
                        ResourceName = "Terraform Execution",
                        Property = "execution_status",
                        ExpectedValue = "success",
                        ActualValue = "error",
                        Severity = errorAnalysis.severity,
                        Description = errorAnalysis.errorMessage,
                        Recommendation = errorAnalysis.recommendation,
                        Category = "configuration"
                    });
                }
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error analyzing terraform failure: {ex.Message}";
                result.Summary = $"❌ Error en '{pipelineName}': El pipeline de terraform falló. Revise los logs para más detalles.";
                result.OverallRisk = "HIGH";
                result.ActionRequired = true;
                return result;
            }
        }

        // Check for "No changes" in the output (successful plan with no drift)
        if (terraformPlanOutput.Contains("No changes", StringComparison.OrdinalIgnoreCase) &&
            terraformPlanOutput.Contains("infrastructure matches", StringComparison.OrdinalIgnoreCase))
        {
            result.Success = true;
            result.Summary = $"✅ No se detectó drift en '{pipelineName}'. La infraestructura está sincronizada con el código Terraform.";
            result.OverallRisk = "NONE";
            result.ActionRequired = false;
            result.PlanSummary = "No changes. Infrastructure is up-to-date.";
            return result;
        }

        // Extract plan summary
        var planMatch = System.Text.RegularExpressions.Regex.Match(
            terraformPlanOutput,
            @"Plan:\s*(\d+)\s*to add,\s*(\d+)\s*to change,\s*(\d+)\s*to destroy",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (planMatch.Success)
        {
            result.PlanSummary = planMatch.Value;
        }

        try
        {
            var client = new AzureOpenAIClient(new Uri(_endpoint), new ApiKeyCredential(_apiKey));
            var chatClient = client.GetChatClient(_deploymentName);

            var driftItems = await AnalyzeWithAIAsync(chatClient, terraformPlanOutput, pipelineName, cancellationToken);

            result.Success = true;
            result.DriftItems = driftItems;
            result.OverallRisk = DetermineOverallRisk(driftItems);
            result.ActionRequired = driftItems.Any(d => d.Severity == "CRITICAL" || d.Severity == "HIGH");
            result.Summary = GenerateSummary(driftItems, pipelineName, result.PlanSummary);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"AI analysis failed: {ex.Message}";
        }

        return result;
    }

    private async Task<List<DriftItem>> AnalyzeWithAIAsync(
        ChatClient chatClient,
        string planOutput,
        string pipelineName,
        CancellationToken cancellationToken)
    {
        var systemPrompt = @"You are an expert in Terraform and Infrastructure as Code (IaC). Your task is to analyze the output of a 'terraform plan' and explain to the user what changes (drift) were detected between the code and the actual infrastructure.

For each detected change, provide:
1. **ResourceId**: The resource identifier (e.g., azurerm_storage_account.main)
2. **ResourceType**: The resource type (e.g., azurerm_storage_account)
3. **ResourceName**: The logical resource name
4. **Property**: The property that changed
5. **ExpectedValue**: What the Terraform code defines
6. **ActualValue**: What currently exists in Azure
7. **Severity**: Severity level
   - CRITICAL: Resource destructions, severe security changes
   - HIGH: Resource replacements (destroy + create), network changes
   - MEDIUM: Significant in-place updates
   - LOW: Minor changes, tags, metadata
8. **Category**: Change category
   - security: Firewall, permissions, encryption changes
   - performance: SKU, tier, capacity changes
   - cost: Changes affecting costs
   - configuration: General configuration changes
9. **Description**: Clear and concise explanation of the issue
10. **Recommendation**: Recommended action

IMPORTANT:
- Analyze ALL lines showing resources to create (+), modify (~), or destroy (-)
- Pay special attention to resources marked as 'must be replaced' or 'forces replacement'
- Identify sensitive changes like passwords, keys, or security configurations
- Respond ONLY with a valid JSON array of objects
- If there are many similar changes, group them logically

Example response:
[
  {
    ""ResourceId"": ""azurerm_storage_account.main"",
    ""ResourceType"": ""azurerm_storage_account"",
    ""ResourceName"": ""main"",
    ""Property"": ""account_replication_type"",
    ""ExpectedValue"": ""GRS"",
    ""ActualValue"": ""LRS"",
    ""Severity"": ""MEDIUM"",
    ""Category"": ""configuration"",
    ""Description"": ""The storage account replication type changed from LRS to GRS, which will improve geo-redundancy."",
    ""Recommendation"": ""Review cost impact before applying. GRS has a higher cost than LRS.""
  }
]";

        // Truncate plan output if too long
        var truncatedPlan = planOutput.Length > 15000 
            ? planOutput[..15000] + "\n\n... [OUTPUT TRUNCATED]" 
            : planOutput;

        var userPrompt = $@"Analyze the following terraform plan output for pipeline '{pipelineName}':

```
{truncatedPlan}
```

Identify all changes (drift) and respond with the JSON array:";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = MaxResponseTokens,
            Temperature = 0.2f // Lower temperature for more consistent parsing
        };

        var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var content = response.Value.Content[0].Text;

        // Clean up the response
        content = CleanJsonResponse(content);

        try
        {
            var driftItems = JsonSerializer.Deserialize<List<DriftItem>>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return driftItems ?? new List<DriftItem>();
        }
        catch (JsonException)
        {
            // If parsing fails, try to extract meaningful error
            return new List<DriftItem>();
        }
    }

    private string CleanJsonResponse(string content)
    {
        content = content.Trim();
        
        // Remove markdown code blocks
        if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            content = content[7..];
        else if (content.StartsWith("```"))
            content = content[3..];
            
        if (content.EndsWith("```"))
            content = content[..^3];
            
        return content.Trim();
    }

    private async Task<(string errorMessage, string summary, string severity, string recommendation)> AnalyzeErrorWithAIAsync(
        ChatClient chatClient,
        string logOutput,
        string pipelineName,
        CancellationToken cancellationToken)
    {
        var errorSystemPrompt = @"You are an expert in Terraform, Azure DevOps, and Infrastructure as Code. Your task is to analyze the logs of a failed terraform pipeline and explain to the user:

1. What was the main error?
2. Why did it occur?
3. How can it be fixed?

Respond in JSON format with the following structure:
{
    ""errorMessage"": ""Clear and concise error description (max 200 characters)"",
    ""summary"": ""Summary for the user, including the pipeline name and a friendly explanation of the problem (max 500 characters)"",
    ""severity"": ""CRITICAL|HIGH|MEDIUM|LOW"",
    ""recommendation"": ""Concrete steps to fix the problem""
}

Common error examples:
- Azure authentication error (expired credentials, insufficient permissions)
- Terraform state error (state lock, corrupted state)
- Configuration error (missing variables, incorrect syntax)
- Resource error (quota exceeded, duplicate name)
- Network or timeout error

IMPORTANT: Respond ONLY with the JSON, no additional explanations.";

        // Truncate log output to avoid token limits
        var truncatedLog = logOutput.Length > 15000 
            ? logOutput[..7500] + "\n\n... [truncated] ...\n\n" + logOutput[^7500..] 
            : logOutput;

        var userPrompt = $@"Analyze the following logs from the failed pipeline '{pipelineName}':

{truncatedLog}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(errorSystemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 1000,
            Temperature = 0.3f
        };

        var completion = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var content = completion.Value.Content[0].Text;
        
        content = CleanJsonResponse(content);

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(content);
            var root = doc.RootElement;
            
            var errorMessage = root.TryGetProperty("errorMessage", out var em) ? em.GetString() ?? "" : "";
            var summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
            var severity = root.TryGetProperty("severity", out var sev) ? sev.GetString() ?? "HIGH" : "HIGH";
            var recommendation = root.TryGetProperty("recommendation", out var rec) ? rec.GetString() ?? "" : "";
            
            // If summary is empty, create a default one
            if (string.IsNullOrEmpty(summary))
            {
                summary = $"❌ Error in '{pipelineName}': {errorMessage}";
            }
            
            return (errorMessage, summary, severity, recommendation);
        }
        catch
        {
            // Fallback if JSON parsing fails
            return (
                "Error executing terraform plan",
                $"❌ Error in '{pipelineName}': Pipeline failed during execution. Check the logs for details.",
                "HIGH",
                "Review the pipeline logs in Azure DevOps to identify the specific error."
            );
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

    private string GenerateSummary(List<DriftItem> items, string pipelineName, string? planSummary)
    {
        var sb = new System.Text.StringBuilder();

        if (!items.Any())
        {
            sb.AppendLine($"✅ No significant issues detected in '{pipelineName}'.");
            return sb.ToString();
        }

        var criticalCount = items.Count(i => i.Severity == "CRITICAL");
        var highCount = items.Count(i => i.Severity == "HIGH");
        var mediumCount = items.Count(i => i.Severity == "MEDIUM");
        var lowCount = items.Count(i => i.Severity == "LOW");

        sb.AppendLine($"## Drift Analysis - {pipelineName}");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(planSummary))
        {
            sb.AppendLine($"📋 **Plan:** {planSummary}");
            sb.AppendLine();
        }

        sb.AppendLine($"### Change Summary");
        sb.AppendLine($"- **Total drift items detected:** {items.Count}");
        
        if (criticalCount > 0) sb.AppendLine($"- ⛔ **Critical:** {criticalCount}");
        if (highCount > 0) sb.AppendLine($"- 🔴 **High:** {highCount}");
        if (mediumCount > 0) sb.AppendLine($"- 🟡 **Medium:** {mediumCount}");
        if (lowCount > 0) sb.AppendLine($"- 🔵 **Low:** {lowCount}");

        // Group by category
        var categories = items.GroupBy(i => i.Category)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{GetCategoryEmoji(g.Key)} {FormatCategory(g.Key)}: {g.Count()}")
            .ToList();

        sb.AppendLine();
        sb.AppendLine($"### Categories");
        foreach (var cat in categories)
        {
            sb.AppendLine($"- {cat}");
        }

        if (criticalCount > 0 || highCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("⚠️ **Action required:** Review critical and high priority changes before applying.");
        }

        return sb.ToString();
    }

    private string GetCategoryEmoji(string category) => category.ToLower() switch
    {
        "security" => "🔒",
        "performance" => "⚡",
        "cost" => "💰",
        "compliance" => "📋",
        "configuration" => "⚙️",
        _ => "📦"
    };

    private string FormatCategory(string category) => category.ToLower() switch
    {
        "security" => "Security",
        "performance" => "Performance",
        "cost" => "Cost",
        "compliance" => "Compliance",
        "configuration" => "Configuration",
        _ => category
    };
}

/// <summary>
/// Mock implementation for local development without Azure OpenAI
/// </summary>
public class MockTerraformPlanAnalyzer : ITerraformPlanAnalyzer
{
    public Task<TerraformPlanAnalysisResult> AnalyzePlanOutputAsync(
        string terraformPlanOutput,
        string pipelineName,
        CancellationToken cancellationToken = default)
    {
        var result = new TerraformPlanAnalysisResult { Success = true };

        // Basic parsing without AI
        var driftItems = ParseTerraformPlanBasic(terraformPlanOutput);
        
        result.DriftItems = driftItems;
        result.OverallRisk = driftItems.Any() 
            ? (driftItems.Any(d => d.Severity == "CRITICAL") ? "CRITICAL" 
               : driftItems.Any(d => d.Severity == "HIGH") ? "HIGH" : "MEDIUM")
            : "NONE";
        result.ActionRequired = driftItems.Any(d => d.Severity == "CRITICAL" || d.Severity == "HIGH");
        result.Summary = $"[Mock] Found {driftItems.Count} changes in terraform plan for '{pipelineName}'";

        // Extract plan summary
        var planMatch = System.Text.RegularExpressions.Regex.Match(
            terraformPlanOutput,
            @"Plan:\s*(\d+)\s*to add,\s*(\d+)\s*to change,\s*(\d+)\s*to destroy",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (planMatch.Success)
        {
            result.PlanSummary = planMatch.Value;
        }

        return Task.FromResult(result);
    }

    private List<DriftItem> ParseTerraformPlanBasic(string planOutput)
    {
        var driftItems = new List<DriftItem>();

        if (string.IsNullOrEmpty(planOutput)) return driftItems;

        // Check for no changes
        if (planOutput.Contains("No changes", StringComparison.OrdinalIgnoreCase))
            return driftItems;

        // Parse resource changes: # resource_type.name will be action
        var resourcePattern = new System.Text.RegularExpressions.Regex(
            @"#\s*([\w_]+\.[\w_\[\]""0-9]+)\s+will be\s+(created|updated|destroyed|replaced)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (System.Text.RegularExpressions.Match match in resourcePattern.Matches(planOutput))
        {
            var resourceAddress = match.Groups[1].Value;
            var action = match.Groups[2].Value.ToLower();

            var parts = resourceAddress.Split('.');
            var resourceType = parts.Length > 0 ? parts[0] : resourceAddress;
            var resourceName = parts.Length > 1 ? string.Join(".", parts.Skip(1)) : resourceAddress;

            var severity = action switch
            {
                "destroyed" => "CRITICAL",
                "replaced" => "HIGH",
                "updated" => "MEDIUM",
                "created" => "LOW",
                _ => "INFO"
            };

            driftItems.Add(new DriftItem
            {
                ResourceId = resourceAddress,
                ResourceType = resourceType,
                ResourceName = resourceName,
                Property = "terraform_state",
                ExpectedValue = "as defined in code",
                ActualValue = action,
                Severity = severity,
                Description = $"[Mock] Resource {resourceName} will be {action}",
                Recommendation = $"Review the terraform plan and apply if changes are expected.",
                Category = severity == "CRITICAL" ? "security" : "configuration"
            });
        }

        return driftItems;
    }
}
