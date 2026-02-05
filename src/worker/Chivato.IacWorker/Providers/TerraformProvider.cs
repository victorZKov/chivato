using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chivato.Shared.Models.Messages;
using CliWrap;
using CliWrap.Buffered;

namespace Chivato.IacWorker.Providers;

/// <summary>
/// Terraform provider for IaC drift analysis
/// </summary>
public class TerraformProvider : IIacProvider
{
    private readonly ILogger<TerraformProvider> _logger;

    public TerraformProvider(ILogger<TerraformProvider> logger)
    {
        _logger = logger;
    }

    public string Name => "terraform";
    public string DisplayName => "Terraform";
    public IReadOnlyList<string> SupportedExtensions => new[] { ".tf", ".tf.json" };

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Cli.Wrap("terraform")
                .WithArguments("version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Cli.Wrap("terraform")
                .WithArguments(new[] { "version", "-json" })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            if (result.ExitCode == 0)
            {
                var versionInfo = JsonSerializer.Deserialize<TerraformVersionOutput>(result.StandardOutput);
                return versionInfo?.TerraformVersion;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IacInitResult> InitializeAsync(
        string workingDirectory,
        IacEnvironmentConfig config,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Running terraform init in {Directory}", workingDirectory);

            progress?.Report(new IacProgressEvent
            {
                Stage = "initializing",
                Progress = 20,
                Message = "Running terraform init..."
            });

            // Build init arguments
            var args = new List<string> { "init", "-no-color", "-input=false" };

            // Add backend config
            foreach (var (key, value) in config.BackendConfig)
            {
                args.Add($"-backend-config={key}={value}");
            }

            // Skip backend initialization if reconfiguring
            if (config.BackendConfig.Count > 0)
            {
                args.Add("-reconfigure");
            }

            var result = await Cli.Wrap("terraform")
                .WithWorkingDirectory(workingDirectory)
                .WithArguments(args)
                .WithEnvironmentVariables(config.EnvironmentVariables)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            stopwatch.Stop();

            if (result.ExitCode != 0)
            {
                _logger.LogError("Terraform init failed: {Error}", result.StandardError);
                return new IacInitResult
                {
                    Success = false,
                    ErrorMessage = result.StandardError,
                    Output = result.StandardOutput,
                    Duration = stopwatch.Elapsed
                };
            }

            _logger.LogInformation("Terraform init completed successfully");

            progress?.Report(new IacProgressEvent
            {
                Stage = "initializing",
                Progress = 35,
                Message = "Terraform initialized successfully"
            });

            return new IacInitResult
            {
                Success = true,
                Output = result.StandardOutput,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terraform init failed with exception");
            return new IacInitResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<IacAnalysisResult> AnalyzeDriftAsync(
        string workingDirectory,
        IacEnvironmentConfig config,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Running terraform plan in {Directory}", workingDirectory);

            progress?.Report(new IacProgressEvent
            {
                Stage = "planning",
                Progress = 50,
                Message = "Running terraform plan..."
            });

            // Build plan arguments
            var args = new List<string> { "plan", "-no-color", "-input=false", "-detailed-exitcode", "-out=tfplan" };

            // Add variables
            foreach (var (key, value) in config.Variables)
            {
                args.Add("-var");
                args.Add($"{key}={value}");
            }

            // Add variable files
            foreach (var varFile in config.VariableFiles)
            {
                args.Add($"-var-file={varFile}");
            }

            // Add targets
            foreach (var target in config.Targets)
            {
                args.Add($"-target={target}");
            }

            var planResult = await Cli.Wrap("terraform")
                .WithWorkingDirectory(workingDirectory)
                .WithArguments(args)
                .WithEnvironmentVariables(config.EnvironmentVariables)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            // Exit codes: 0 = no changes, 1 = error, 2 = changes detected
            if (planResult.ExitCode == 1)
            {
                _logger.LogError("Terraform plan failed: {Error}", planResult.StandardError);
                return new IacAnalysisResult
                {
                    Success = false,
                    ErrorMessage = planResult.StandardError,
                    RawOutput = planResult.StandardOutput,
                    Duration = stopwatch.Elapsed
                };
            }

            progress?.Report(new IacProgressEvent
            {
                Stage = "analyzing",
                Progress = 70,
                Message = "Parsing terraform plan output..."
            });

            // Get JSON output for detailed analysis
            var showResult = await Cli.Wrap("terraform")
                .WithWorkingDirectory(workingDirectory)
                .WithArguments(new[] { "show", "-json", "tfplan" })
                .WithEnvironmentVariables(config.EnvironmentVariables)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            stopwatch.Stop();

            if (showResult.ExitCode != 0)
            {
                _logger.LogError("Terraform show failed: {Error}", showResult.StandardError);
                return new IacAnalysisResult
                {
                    Success = false,
                    ErrorMessage = showResult.StandardError,
                    RawOutput = planResult.StandardOutput,
                    Duration = stopwatch.Elapsed
                };
            }

            // Parse the JSON plan output
            var drifts = ParsePlanOutput(showResult.StandardOutput);

            _logger.LogInformation("Terraform plan completed. Found {DriftCount} changes", drifts.Count);

            progress?.Report(new IacProgressEvent
            {
                Stage = "completed",
                Progress = 90,
                Message = $"Analysis complete. Found {drifts.Count} changes"
            });

            return new IacAnalysisResult
            {
                Success = true,
                RawOutput = planResult.StandardOutput,
                Drifts = drifts,
                Summary = CalculateSummary(drifts),
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terraform plan failed with exception");
            return new IacAnalysisResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<IacValidationResult> ValidateAsync(
        string workingDirectory,
        IacEnvironmentConfig config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await Cli.Wrap("terraform")
                .WithWorkingDirectory(workingDirectory)
                .WithArguments(new[] { "validate", "-json" })
                .WithEnvironmentVariables(config.EnvironmentVariables)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            var validation = JsonSerializer.Deserialize<TerraformValidateOutput>(result.StandardOutput);

            return new IacValidationResult
            {
                IsValid = validation?.Valid ?? false,
                Errors = validation?.Diagnostics?
                    .Where(d => d.Severity == "error")
                    .Select(d => new IacValidationError
                    {
                        File = d.Range?.Filename ?? "",
                        Line = d.Range?.Start?.Line,
                        Message = d.Summary + (string.IsNullOrEmpty(d.Detail) ? "" : $": {d.Detail}")
                    })
                    .ToList() ?? new List<IacValidationError>(),
                Warnings = validation?.Diagnostics?
                    .Where(d => d.Severity == "warning")
                    .Select(d => new IacValidationWarning
                    {
                        File = d.Range?.Filename ?? "",
                        Line = d.Range?.Start?.Line,
                        Message = d.Summary + (string.IsNullOrEmpty(d.Detail) ? "" : $": {d.Detail}")
                    })
                    .ToList() ?? new List<IacValidationWarning>()
            };
        }
        catch (Exception ex)
        {
            return new IacValidationResult
            {
                IsValid = false,
                Errors = new List<IacValidationError>
                {
                    new() { Message = ex.Message }
                }
            };
        }
    }

    private List<IacDrift> ParsePlanOutput(string jsonOutput)
    {
        var drifts = new List<IacDrift>();

        try
        {
            var plan = JsonSerializer.Deserialize<TerraformPlanOutput>(jsonOutput, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (plan?.ResourceChanges == null)
                return drifts;

            foreach (var change in plan.ResourceChanges)
            {
                // Skip no-op changes
                if (change.Change?.Actions == null ||
                    change.Change.Actions.All(a => a == "no-op" || a == "read"))
                    continue;

                var action = DetermineAction(change.Change.Actions);
                var severity = DetermineSeverity(action, change.Type);

                var drift = new IacDrift
                {
                    ResourceAddress = change.Address,
                    ResourceType = change.Type,
                    ResourceName = change.Name,
                    Action = action,
                    Severity = severity,
                    Category = CategorizeResourceType(change.Type),
                    Description = GenerateDescription(action, change),
                    Recommendation = GenerateRecommendation(action, change),
                    PropertyChanges = ExtractPropertyChanges(change.Change)
                };

                drifts.Add(drift);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse terraform plan output");
        }

        return drifts;
    }

    private string DetermineAction(List<string> actions)
    {
        if (actions.Contains("delete") && actions.Contains("create"))
            return "replace";
        if (actions.Contains("delete"))
            return "delete";
        if (actions.Contains("create"))
            return "create";
        if (actions.Contains("update"))
            return "update";
        return "unknown";
    }

    private string DetermineSeverity(string action, string resourceType)
    {
        // Deletions and replacements are high severity
        if (action == "delete" || action == "replace")
            return "HIGH";

        // Security-related resources are critical
        var securityResources = new[] { "firewall", "security_group", "policy", "role", "identity", "vault" };
        if (securityResources.Any(r => resourceType.Contains(r, StringComparison.OrdinalIgnoreCase)))
            return "CRITICAL";

        // Network changes are high
        var networkResources = new[] { "network", "subnet", "route", "gateway", "lb", "load_balancer" };
        if (networkResources.Any(r => resourceType.Contains(r, StringComparison.OrdinalIgnoreCase)))
            return "HIGH";

        return "MEDIUM";
    }

    private string CategorizeResourceType(string resourceType)
    {
        if (resourceType.Contains("security") || resourceType.Contains("policy") ||
            resourceType.Contains("role") || resourceType.Contains("identity"))
            return "security";

        if (resourceType.Contains("network") || resourceType.Contains("subnet") ||
            resourceType.Contains("gateway") || resourceType.Contains("lb"))
            return "network";

        if (resourceType.Contains("storage") || resourceType.Contains("blob") ||
            resourceType.Contains("container"))
            return "storage";

        if (resourceType.Contains("database") || resourceType.Contains("sql") ||
            resourceType.Contains("cosmos"))
            return "database";

        if (resourceType.Contains("app") || resourceType.Contains("function") ||
            resourceType.Contains("container_app"))
            return "compute";

        return "configuration";
    }

    private string GenerateDescription(string action, TerraformResourceChange change)
    {
        return action switch
        {
            "create" => $"Resource {change.Address} will be created (exists in IaC but not in Azure)",
            "delete" => $"Resource {change.Address} will be destroyed (exists in Azure but not in IaC)",
            "update" => $"Resource {change.Address} will be updated (configuration drift detected)",
            "replace" => $"Resource {change.Address} will be replaced (recreation required due to changes)",
            _ => $"Resource {change.Address} has changes"
        };
    }

    private string GenerateRecommendation(string action, TerraformResourceChange change)
    {
        return action switch
        {
            "create" => "Run terraform apply to create the missing resource in Azure",
            "delete" => "Either add this resource to your Terraform configuration or run terraform apply to remove it",
            "update" => "Review the changes and run terraform apply if intended, or update the IaC to match current state",
            "replace" => "This change will cause downtime. Review carefully and consider using lifecycle rules if needed",
            _ => "Review the terraform plan output for details"
        };
    }

    private List<IacPropertyChange> ExtractPropertyChanges(TerraformChange? change)
    {
        var changes = new List<IacPropertyChange>();

        if (change?.Before == null && change?.After == null)
            return changes;

        try
        {
            var beforeDict = change.Before != null
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(change.Before.Value.GetRawText())
                : new Dictionary<string, JsonElement>();

            var afterDict = change.After != null
                ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(change.After.Value.GetRawText())
                : new Dictionary<string, JsonElement>();

            var allKeys = beforeDict!.Keys.Union(afterDict!.Keys).ToHashSet();

            foreach (var key in allKeys)
            {
                var hasBefore = beforeDict.TryGetValue(key, out var beforeVal);
                var hasAfter = afterDict.TryGetValue(key, out var afterVal);

                var beforeStr = hasBefore ? beforeVal.ToString() : null;
                var afterStr = hasAfter ? afterVal.ToString() : null;

                if (beforeStr != afterStr)
                {
                    var isSensitive = change.AfterSensitive != null &&
                        change.AfterSensitive.Value.TryGetProperty(key, out var sensVal) &&
                        sensVal.ValueKind == JsonValueKind.True;

                    changes.Add(new IacPropertyChange
                    {
                        PropertyPath = key,
                        ExpectedValue = isSensitive ? "(sensitive)" : afterStr,
                        ActualValue = isSensitive ? "(sensitive)" : beforeStr,
                        IsSensitive = isSensitive
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract property changes");
        }

        return changes;
    }

    private IacDriftSummary CalculateSummary(List<IacDrift> drifts)
    {
        return new IacDriftSummary
        {
            ToAdd = drifts.Count(d => d.Action == "create"),
            ToChange = drifts.Count(d => d.Action == "update"),
            ToDestroy = drifts.Count(d => d.Action == "delete"),
            ToReplace = drifts.Count(d => d.Action == "replace"),
            Critical = drifts.Count(d => d.Severity == "CRITICAL"),
            High = drifts.Count(d => d.Severity == "HIGH"),
            Medium = drifts.Count(d => d.Severity == "MEDIUM"),
            Low = drifts.Count(d => d.Severity == "LOW")
        };
    }
}

#region Terraform JSON Models

internal class TerraformVersionOutput
{
    [JsonPropertyName("terraform_version")]
    public string TerraformVersion { get; set; } = string.Empty;
}

internal class TerraformValidateOutput
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("diagnostics")]
    public List<TerraformDiagnostic>? Diagnostics { get; set; }
}

internal class TerraformDiagnostic
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }

    [JsonPropertyName("range")]
    public TerraformRange? Range { get; set; }
}

internal class TerraformRange
{
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public TerraformPosition? Start { get; set; }
}

internal class TerraformPosition
{
    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("column")]
    public int Column { get; set; }
}

internal class TerraformPlanOutput
{
    [JsonPropertyName("resource_changes")]
    public List<TerraformResourceChange>? ResourceChanges { get; set; }
}

internal class TerraformResourceChange
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("provider_name")]
    public string ProviderName { get; set; } = string.Empty;

    [JsonPropertyName("change")]
    public TerraformChange? Change { get; set; }
}

internal class TerraformChange
{
    [JsonPropertyName("actions")]
    public List<string> Actions { get; set; } = new();

    [JsonPropertyName("before")]
    public JsonElement? Before { get; set; }

    [JsonPropertyName("after")]
    public JsonElement? After { get; set; }

    [JsonPropertyName("after_sensitive")]
    public JsonElement? AfterSensitive { get; set; }
}

#endregion
