using System.Text.RegularExpressions;
using Chivato.IacWorker.Providers;

namespace Chivato.IacWorker.Services;

/// <summary>
/// Parses terraform plan output from pipeline logs to extract drift information
/// </summary>
public class TerraformPlanParser
{
    // Regex patterns for terraform plan output
    private static readonly Regex PlanSummaryPattern = new(
        @"Plan:\s*(\d+)\s*to add,\s*(\d+)\s*to change,\s*(\d+)\s*to destroy",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NoChangesPattern = new(
        @"No changes\.\s*(Your infrastructure matches the configuration|Infrastructure is up-to-date)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ResourceActionPattern = new(
        @"#\s*([\w\.\[\]""_-]+)\s+(will be|must be)\s+(created|destroyed|updated|replaced)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ResourceChangeBlockPattern = new(
        @"#\s*([\w\.\[\]""_-]+)\s+(will be|must be)\s+(created|destroyed|updated|replaced)[\s\S]*?(?=#\s*[\w\.\[\]""_-]+\s+(?:will be|must be)|Plan:|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PropertyChangePattern = new(
        @"~\s*([\w_]+)\s*=\s*""?([^""]+)""?\s*->\s*""?([^""]+)""?",
        RegexOptions.Compiled);

    private static readonly Regex PropertyAddPattern = new(
        @"\+\s*([\w_]+)\s*=\s*""?([^""]+)""?",
        RegexOptions.Compiled);

    private static readonly Regex PropertyRemovePattern = new(
        @"-\s*([\w_]+)\s*=\s*""?([^""]+)""?",
        RegexOptions.Compiled);

    /// <summary>
    /// Parse terraform plan output from log content
    /// </summary>
    public TerraformPlanResult Parse(string logContent)
    {
        var result = new TerraformPlanResult();

        if (string.IsNullOrWhiteSpace(logContent))
        {
            result.Success = false;
            result.ErrorMessage = "Empty log content";
            return result;
        }

        // Check if there are no changes
        if (NoChangesPattern.IsMatch(logContent))
        {
            result.Success = true;
            result.HasChanges = false;
            result.Summary = new IacDriftSummary();
            return result;
        }

        // Try to find the plan summary line
        var summaryMatch = PlanSummaryPattern.Match(logContent);
        if (summaryMatch.Success)
        {
            result.Success = true;
            result.HasChanges = true;
            result.Summary = new IacDriftSummary
            {
                ToAdd = int.Parse(summaryMatch.Groups[1].Value),
                ToChange = int.Parse(summaryMatch.Groups[2].Value),
                ToDestroy = int.Parse(summaryMatch.Groups[3].Value)
            };
        }

        // Parse individual resource changes
        var resourceMatches = ResourceActionPattern.Matches(logContent);
        foreach (Match match in resourceMatches)
        {
            var resourceAddress = match.Groups[1].Value;
            var action = NormalizeAction(match.Groups[3].Value);

            var drift = new IacDrift
            {
                ResourceAddress = resourceAddress,
                ResourceType = ExtractResourceType(resourceAddress),
                ResourceName = ExtractResourceName(resourceAddress),
                Action = action,
                Severity = DetermineSeverity(action, resourceAddress),
                Category = CategorizeResource(resourceAddress),
                Description = GenerateDescription(action, resourceAddress),
                Recommendation = GenerateRecommendation(action)
            };

            // Try to extract property changes for this resource
            drift.PropertyChanges = ExtractPropertyChanges(logContent, resourceAddress);

            result.Drifts.Add(drift);
        }

        // Update severity counts
        if (result.Summary != null)
        {
            result.Summary.Critical = result.Drifts.Count(d => d.Severity == "CRITICAL");
            result.Summary.High = result.Drifts.Count(d => d.Severity == "HIGH");
            result.Summary.Medium = result.Drifts.Count(d => d.Severity == "MEDIUM");
            result.Summary.Low = result.Drifts.Count(d => d.Severity == "LOW");
        }

        // If we found resources but no summary, count them
        if (result.Summary == null && result.Drifts.Count > 0)
        {
            result.Success = true;
            result.HasChanges = true;
            result.Summary = new IacDriftSummary
            {
                ToAdd = result.Drifts.Count(d => d.Action == "create"),
                ToChange = result.Drifts.Count(d => d.Action == "update"),
                ToDestroy = result.Drifts.Count(d => d.Action == "delete"),
                ToReplace = result.Drifts.Count(d => d.Action == "replace")
            };
        }

        // Check for errors in the log
        if (logContent.Contains("Error:") && !result.Success)
        {
            result.Success = false;
            result.ErrorMessage = ExtractErrorMessage(logContent);
        }

        return result;
    }

    /// <summary>
    /// Check if the log contains terraform plan output
    /// </summary>
    public bool ContainsTerraformPlan(string logContent)
    {
        if (string.IsNullOrWhiteSpace(logContent)) return false;

        // Look for terraform plan indicators
        return logContent.Contains("Terraform will perform the following actions") ||
               logContent.Contains("Plan:") ||
               NoChangesPattern.IsMatch(logContent) ||
               logContent.Contains("terraform plan") ||
               ResourceActionPattern.IsMatch(logContent);
    }

    private string NormalizeAction(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "created" => "create",
            "destroyed" => "delete",
            "updated" => "update",
            "replaced" => "replace",
            _ => action.ToLowerInvariant()
        };
    }

    private string ExtractResourceType(string resourceAddress)
    {
        // Format: module.name.resource_type.name or resource_type.name
        var parts = resourceAddress.Split('.');
        if (parts.Length >= 2)
        {
            // Skip module parts
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!parts[i].StartsWith("module"))
                {
                    return parts[i];
                }
            }
            return parts[^2]; // Second to last
        }
        return resourceAddress;
    }

    private string ExtractResourceName(string resourceAddress)
    {
        var parts = resourceAddress.Split('.');
        return parts.Length > 0 ? parts[^1] : resourceAddress;
    }

    private string DetermineSeverity(string action, string resourceAddress)
    {
        // Deletions and replacements are high severity
        if (action == "delete" || action == "replace")
            return "HIGH";

        // Security resources are critical
        var securityResources = new[] { "firewall", "security_group", "policy", "role", "identity", "vault", "key" };
        if (securityResources.Any(r => resourceAddress.Contains(r, StringComparison.OrdinalIgnoreCase)))
            return "CRITICAL";

        // Network resources are high
        var networkResources = new[] { "network", "subnet", "route", "gateway", "lb", "load_balancer" };
        if (networkResources.Any(r => resourceAddress.Contains(r, StringComparison.OrdinalIgnoreCase)))
            return "HIGH";

        // Creation of new resources is medium
        if (action == "create")
            return "MEDIUM";

        return "MEDIUM";
    }

    private string CategorizeResource(string resourceAddress)
    {
        var address = resourceAddress.ToLowerInvariant();

        if (address.Contains("security") || address.Contains("policy") ||
            address.Contains("role") || address.Contains("identity") ||
            address.Contains("key") || address.Contains("vault"))
            return "security";

        if (address.Contains("network") || address.Contains("subnet") ||
            address.Contains("gateway") || address.Contains("lb") ||
            address.Contains("firewall"))
            return "network";

        if (address.Contains("storage") || address.Contains("blob") ||
            address.Contains("container") || address.Contains("account"))
            return "storage";

        if (address.Contains("database") || address.Contains("sql") ||
            address.Contains("cosmos") || address.Contains("redis"))
            return "database";

        if (address.Contains("app") || address.Contains("function") ||
            address.Contains("vm") || address.Contains("container"))
            return "compute";

        return "configuration";
    }

    private string GenerateDescription(string action, string resourceAddress)
    {
        return action switch
        {
            "create" => $"Resource {resourceAddress} needs to be created (defined in IaC but missing in Azure)",
            "delete" => $"Resource {resourceAddress} will be destroyed (exists in Azure but removed from IaC)",
            "update" => $"Resource {resourceAddress} has configuration drift (Azure state differs from IaC)",
            "replace" => $"Resource {resourceAddress} must be replaced (changes require recreation)",
            _ => $"Resource {resourceAddress} has changes"
        };
    }

    private string GenerateRecommendation(string action)
    {
        return action switch
        {
            "create" => "Review and apply terraform to create the resource, or update IaC if resource should not exist",
            "delete" => "If resource should exist, add it back to IaC. Otherwise, apply terraform to remove it",
            "update" => "Review the drift. If Azure is correct, update IaC. If IaC is correct, apply terraform",
            "replace" => "Warning: Resource will be destroyed and recreated. Consider using lifecycle rules if downtime is a concern",
            _ => "Review the terraform plan output for details"
        };
    }

    private List<IacPropertyChange> ExtractPropertyChanges(string logContent, string resourceAddress)
    {
        var changes = new List<IacPropertyChange>();

        // Try to find the block for this resource
        // This is a simplified extraction - terraform plan format can be complex
        try
        {
            var escapedAddress = Regex.Escape(resourceAddress);
            var blockPattern = new Regex(
                $@"#\s*{escapedAddress}[\s\S]*?(?=#\s*[\w\.\[\]""_-]+\s+(?:will be|must be)|Plan:|$)",
                RegexOptions.IgnoreCase);

            var blockMatch = blockPattern.Match(logContent);
            if (!blockMatch.Success) return changes;

            var block = blockMatch.Value;

            // Find property changes (~)
            foreach (Match match in PropertyChangePattern.Matches(block))
            {
                changes.Add(new IacPropertyChange
                {
                    PropertyPath = match.Groups[1].Value,
                    ActualValue = match.Groups[2].Value.Trim(),
                    ExpectedValue = match.Groups[3].Value.Trim(),
                    IsSensitive = match.Groups[2].Value.Contains("sensitive") ||
                                 match.Groups[3].Value.Contains("sensitive")
                });
            }
        }
        catch
        {
            // Property extraction failed, return empty list
        }

        return changes;
    }

    private string ExtractErrorMessage(string logContent)
    {
        var errorPattern = new Regex(@"Error:\s*(.+?)(?:\n\n|\z)", RegexOptions.Singleline);
        var match = errorPattern.Match(logContent);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return "Unknown error in terraform plan";
    }
}

/// <summary>
/// Result of parsing terraform plan output
/// </summary>
public class TerraformPlanResult
{
    public bool Success { get; set; }
    public bool HasChanges { get; set; }
    public string? ErrorMessage { get; set; }
    public IacDriftSummary? Summary { get; set; }
    public List<IacDrift> Drifts { get; set; } = new();
}
