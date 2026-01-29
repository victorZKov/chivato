using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using System.Diagnostics;

namespace Chivato.Worker.Processors;

/// <summary>
/// Processes drift analysis requests
/// </summary>
public class DriftAnalysisProcessor : IDriftAnalysisProcessor
{
    private readonly IStorageService _storageService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly IAdoService _adoService;
    private readonly IAzureResourceService _azureResourceService;
    private readonly ILogger<DriftAnalysisProcessor> _logger;

    public DriftAnalysisProcessor(
        IStorageService storageService,
        IKeyVaultService keyVaultService,
        IAdoService adoService,
        IAzureResourceService azureResourceService,
        ILogger<DriftAnalysisProcessor> logger)
    {
        _storageService = storageService;
        _keyVaultService = keyVaultService;
        _adoService = adoService;
        _azureResourceService = azureResourceService;
        _logger = logger;
    }

    public async Task<DriftAnalysisResultMessage> ProcessAsync(
        DriftAnalysisMessage message,
        IProgress<AnalysisProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new DriftAnalysisResultMessage
        {
            CorrelationId = message.CorrelationId,
            TenantId = message.TenantId
        };

        try
        {
            _logger.LogInformation("Starting drift analysis for correlation {CorrelationId}",
                message.CorrelationId);

            // Get pipelines to analyze
            var pipelines = await GetPipelinesToAnalyzeAsync(message, cancellationToken);

            if (!pipelines.Any())
            {
                result.Status = "Completed";
                result.DriftItemCount = 0;
                result.OverallRisk = "NONE";
                stopwatch.Stop();
                result.ProcessingDuration = stopwatch.Elapsed;
                return result;
            }

            var totalDrifts = 0;
            var overallRisk = "NONE";

            foreach (var pipeline in pipelines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                result.PipelineId = pipeline.RowKey;
                result.PipelineName = pipeline.PipelineName;

                ReportProgress(progress, message, pipeline, "scanning_pipeline", 10,
                    $"Scanning pipeline {pipeline.PipelineName}...");

                // Get credentials
                var adoPat = await _keyVaultService.GetSecretAsync($"ado-pat-{message.TenantId}")
                    ?? await _keyVaultService.GetSecretAsync("ado-pat");

                if (string.IsNullOrEmpty(adoPat))
                {
                    _logger.LogWarning("No ADO PAT found for pipeline {PipelineId}", pipeline.RowKey);
                    continue;
                }

                // Scan pipeline for IaC definitions
                var scanResult = await _adoService.ScanPipelineAsync(
                    pipeline.OrganizationUrl,
                    adoPat,
                    pipeline.ProjectName,
                    pipeline.PipelineId,
                    cancellationToken);

                ReportProgress(progress, message, pipeline, "fetching_resources", 40,
                    "Fetching Azure resource states...");

                if (!scanResult.Success)
                {
                    _logger.LogWarning("Pipeline scan failed: {Error}", scanResult.ErrorMessage);
                    await LogScanAsync(pipeline, "failed", scanResult.ErrorMessage, message.CorrelationId);
                    continue;
                }

                // Get Azure credentials
                var azureConfig = await GetAzureConfigAsync(message.TenantId);
                if (azureConfig == null)
                {
                    _logger.LogWarning("No Azure credentials configured for tenant {TenantId}", message.TenantId);
                    continue;
                }

                // Get current Azure resources
                var resources = new List<AzureResourceState>();
                if (!string.IsNullOrEmpty(pipeline.SubscriptionId) && !string.IsNullOrEmpty(pipeline.ResourceGroup))
                {
                    var resourceList = await _azureResourceService.GetResourcesAsync(
                        azureConfig.TenantId,
                        azureConfig.ClientId,
                        azureConfig.ClientSecret,
                        pipeline.SubscriptionId,
                        pipeline.ResourceGroup,
                        cancellationToken);
                    resources.AddRange(resourceList);
                }

                ReportProgress(progress, message, pipeline, "analyzing", 70,
                    "Analyzing drift...");

                // Analyze drift
                var driftItems = AnalyzeDrift(scanResult, resources);

                // Save drifts
                var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
                foreach (var drift in driftItems)
                {
                    await _storageService.SaveDriftRecordAsync(new DriftRecordEntity
                    {
                        PartitionKey = today,
                        RowKey = Guid.NewGuid().ToString(),
                        PipelineId = pipeline.RowKey,
                        PipelineName = pipeline.PipelineName,
                        ResourceId = drift.ResourceId,
                        ResourceType = drift.ResourceType,
                        ResourceName = drift.ResourceName,
                        Property = drift.Property,
                        ExpectedValue = drift.ExpectedValue,
                        ActualValue = drift.ActualValue,
                        Severity = drift.Severity,
                        Description = drift.Description,
                        Recommendation = drift.Recommendation,
                        Category = drift.Category,
                        Status = "new",
                        TenantId = message.TenantId,
                        CorrelationId = message.CorrelationId,
                        DetectedAt = DateTimeOffset.UtcNow
                    });
                }

                totalDrifts += driftItems.Count;

                // Update risk level
                if (driftItems.Any())
                {
                    var maxSeverity = driftItems.Max(d => GetSeverityWeight(d.Severity));
                    if (maxSeverity > GetSeverityWeight(overallRisk))
                    {
                        overallRisk = GetSeverityFromWeight(maxSeverity);
                    }
                }

                // Update pipeline stats
                pipeline.LastScanAt = DateTimeOffset.UtcNow;
                pipeline.DriftCount = driftItems.Count;
                await _storageService.SavePipelineAsync(pipeline);

                // Log scan
                await LogScanAsync(pipeline, "success", null, message.CorrelationId, driftItems.Count, resources.Count);

                ReportProgress(progress, message, pipeline, "completed", 100,
                    $"Analysis complete. Found {driftItems.Count} drifts.");
            }

            stopwatch.Stop();

            result.Status = "Completed";
            result.DriftItemCount = totalDrifts;
            result.OverallRisk = overallRisk;
            result.ProcessingDuration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Drift analysis completed for correlation {CorrelationId}. Found {DriftCount} drifts. Duration: {Duration}ms",
                message.CorrelationId, totalDrifts, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Status = "Cancelled";
            result.ErrorMessage = "Analysis was cancelled";
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drift analysis failed for correlation {CorrelationId}", message.CorrelationId);
            stopwatch.Stop();
            result.Status = "Failed";
            result.ErrorMessage = ex.Message;
            result.ProcessingDuration = stopwatch.Elapsed;
            return result;
        }
    }

    private async Task<IEnumerable<PipelineEntity>> GetPipelinesToAnalyzeAsync(
        DriftAnalysisMessage message, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(message.PipelineId) && !string.IsNullOrEmpty(message.OrganizationId))
        {
            var pipeline = await _storageService.GetPipelineAsync(message.OrganizationId, message.PipelineId);
            return pipeline != null ? new[] { pipeline } : Array.Empty<PipelineEntity>();
        }

        if (!string.IsNullOrEmpty(message.PipelineId))
        {
            var pipeline = await _storageService.GetPipelineByIdAsync(message.PipelineId);
            return pipeline != null ? new[] { pipeline } : Array.Empty<PipelineEntity>();
        }

        // Get all active pipelines
        var pipelines = await _storageService.GetActivePipelinesAsync();

        // Filter by tenant if specified
        if (!string.IsNullOrEmpty(message.TenantId))
        {
            pipelines = pipelines.Where(p => p.TenantId == message.TenantId);
        }

        return pipelines;
    }

    private async Task<AzureCredentials?> GetAzureConfigAsync(string tenantId)
    {
        var azureTenantId = await _keyVaultService.GetSecretAsync($"azure-tenant-{tenantId}")
            ?? await _keyVaultService.GetSecretAsync("azure-tenant-id");
        var clientId = await _keyVaultService.GetSecretAsync($"azure-client-{tenantId}")
            ?? await _keyVaultService.GetSecretAsync("azure-client-id");
        var clientSecret = await _keyVaultService.GetSecretAsync($"azure-secret-{tenantId}")
            ?? await _keyVaultService.GetSecretAsync("azure-client-secret");

        if (string.IsNullOrEmpty(azureTenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return null;

        return new AzureCredentials
        {
            TenantId = azureTenantId,
            ClientId = clientId,
            ClientSecret = clientSecret
        };
    }

    private List<DriftItem> AnalyzeDrift(PipelineScanResult scanResult, List<AzureResourceState> actualResources)
    {
        var drifts = new List<DriftItem>();

        // Simple drift detection - compare expected vs actual resources
        foreach (var definition in scanResult.InfrastructureDefinitions)
        {
            foreach (var expected in definition.Resources)
            {
                var actual = actualResources.FirstOrDefault(r =>
                    r.Name.Equals(expected.Name, StringComparison.OrdinalIgnoreCase) &&
                    r.ResourceType.Contains(expected.Type, StringComparison.OrdinalIgnoreCase));

                if (actual == null)
                {
                    // Resource missing
                    drifts.Add(new DriftItem
                    {
                        ResourceId = $"expected/{expected.Type}/{expected.Name}",
                        ResourceType = expected.Type,
                        ResourceName = expected.Name,
                        Property = "existence",
                        ExpectedValue = "exists",
                        ActualValue = "missing",
                        Severity = "HIGH",
                        Description = $"Expected resource {expected.Name} of type {expected.Type} is missing",
                        Recommendation = "Re-run the pipeline to deploy the missing resource",
                        Category = "configuration"
                    });
                }
                else
                {
                    // Check property differences
                    foreach (var prop in expected.Properties)
                    {
                        if (actual.Properties.TryGetValue(prop.Key, out var actualValue))
                        {
                            var expectedStr = prop.Value?.ToString() ?? "";
                            var actualStr = actualValue?.ToString() ?? "";

                            if (!expectedStr.Equals(actualStr, StringComparison.OrdinalIgnoreCase))
                            {
                                var severity = DetermineSeverity(prop.Key, expected.Type);
                                drifts.Add(new DriftItem
                                {
                                    ResourceId = actual.ResourceId,
                                    ResourceType = actual.ResourceType,
                                    ResourceName = actual.Name,
                                    Property = prop.Key,
                                    ExpectedValue = expectedStr,
                                    ActualValue = actualStr,
                                    Severity = severity,
                                    Description = $"Property {prop.Key} differs from expected value",
                                    Recommendation = "Review the change and re-run pipeline if needed",
                                    Category = CategorizeProperty(prop.Key)
                                });
                            }
                        }
                    }
                }
            }
        }

        // Check for unexpected resources (not defined in IaC)
        foreach (var actual in actualResources)
        {
            var isExpected = scanResult.InfrastructureDefinitions
                .SelectMany(d => d.Resources)
                .Any(r => r.Name.Equals(actual.Name, StringComparison.OrdinalIgnoreCase));

            if (!isExpected)
            {
                drifts.Add(new DriftItem
                {
                    ResourceId = actual.ResourceId,
                    ResourceType = actual.ResourceType,
                    ResourceName = actual.Name,
                    Property = "existence",
                    ExpectedValue = "not defined",
                    ActualValue = "exists",
                    Severity = "MEDIUM",
                    Description = $"Resource {actual.Name} exists but is not defined in IaC",
                    Recommendation = "Either add to IaC definition or remove if not needed",
                    Category = "configuration"
                });
            }
        }

        return drifts;
    }

    private string DetermineSeverity(string propertyName, string resourceType)
    {
        // Security-related properties are critical
        var securityProps = new[] { "sku", "accessPolicies", "networkRules", "encryption", "identity" };
        if (securityProps.Any(p => propertyName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return "CRITICAL";

        // Performance/scaling properties are high
        var performanceProps = new[] { "capacity", "size", "tier", "replication" };
        if (performanceProps.Any(p => propertyName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return "HIGH";

        // Tags and metadata are low
        var metadataProps = new[] { "tags", "metadata", "description" };
        if (metadataProps.Any(p => propertyName.Contains(p, StringComparison.OrdinalIgnoreCase)))
            return "LOW";

        return "MEDIUM";
    }

    private string CategorizeProperty(string propertyName)
    {
        if (propertyName.Contains("security", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("encryption", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("access", StringComparison.OrdinalIgnoreCase))
            return "security";

        if (propertyName.Contains("sku", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("capacity", StringComparison.OrdinalIgnoreCase))
            return "cost";

        if (propertyName.Contains("size", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("tier", StringComparison.OrdinalIgnoreCase))
            return "performance";

        return "configuration";
    }

    private async Task LogScanAsync(
        PipelineEntity pipeline,
        string status,
        string? error,
        string? correlationId,
        int driftCount = 0,
        int resourcesScanned = 0)
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
        await _storageService.SaveScanLogAsync(new ScanLogEntity
        {
            PartitionKey = today,
            RowKey = Guid.NewGuid().ToString(),
            PipelineId = pipeline.RowKey,
            PipelineName = pipeline.PipelineName,
            TenantId = pipeline.TenantId,
            Status = status,
            DriftCount = driftCount,
            ResourcesScanned = resourcesScanned,
            ErrorMessage = error,
            CorrelationId = correlationId,
            StartedAt = DateTimeOffset.UtcNow,
            TriggeredBy = "worker"
        });
    }

    private void ReportProgress(
        IProgress<AnalysisProgressEvent>? progress,
        DriftAnalysisMessage message,
        PipelineEntity pipeline,
        string stage,
        int percent,
        string msg)
    {
        progress?.Report(new AnalysisProgressEvent
        {
            CorrelationId = message.CorrelationId,
            PipelineId = pipeline.RowKey,
            PipelineName = pipeline.PipelineName,
            TenantId = message.TenantId,
            Stage = stage,
            Progress = percent,
            Message = msg
        });
    }

    private int GetSeverityWeight(string severity) => severity.ToUpperInvariant() switch
    {
        "CRITICAL" => 4,
        "HIGH" => 3,
        "MEDIUM" => 2,
        "LOW" => 1,
        _ => 0
    };

    private string GetSeverityFromWeight(int weight) => weight switch
    {
        4 => "CRITICAL",
        3 => "HIGH",
        2 => "MEDIUM",
        1 => "LOW",
        _ => "NONE"
    };

    private class AzureCredentials
    {
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
