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
    private readonly ITerraformPlanAnalyzer _terraformPlanAnalyzer;
    private readonly ILogger<DriftAnalysisProcessor> _logger;

    public DriftAnalysisProcessor(
        IStorageService storageService,
        IKeyVaultService keyVaultService,
        IAdoService adoService,
        IAzureResourceService azureResourceService,
        ITerraformPlanAnalyzer terraformPlanAnalyzer,
        ILogger<DriftAnalysisProcessor> logger)
    {
        _storageService = storageService;
        _keyVaultService = keyVaultService;
        _adoService = adoService;
        _azureResourceService = azureResourceService;
        _terraformPlanAnalyzer = terraformPlanAnalyzer;
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
            _logger.LogInformation("Starting drift analysis for correlation {CorrelationId}. Tenant: {TenantId}, PipelineId: {PipelineId}, OrganizationId: {OrganizationId}",
                message.CorrelationId, message.TenantId, message.PipelineId, message.OrganizationId);

            // Get pipelines to analyze
            var pipelines = await GetPipelinesToAnalyzeAsync(message, cancellationToken);
            _logger.LogInformation("Found {Count} pipelines to analyze for correlation {CorrelationId}", pipelines?.Count() ?? 0, message.CorrelationId);

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

                // Ensure we have OrganizationUrl and PAT
                string organizationUrl = pipeline.OrganizationUrl;
                string? adoPat = null;

                // Try to get details from ADO Connection linkage if url is missing or we need the PAT
                if (!string.IsNullOrEmpty(pipeline.AdoConnectionId))
                {
                    _logger.LogInformation("Pipeline {PipelineId} is linked to ADO Connection {AdoConnectionId}. Retrieving details...", pipeline.RowKey, pipeline.AdoConnectionId);
                    var adoConnection = await _storageService.GetAdoConnectionAsync(pipeline.AdoConnectionId);
                    if (adoConnection != null)
                    {
                        if (string.IsNullOrEmpty(organizationUrl))
                        {
                            // If OrganizationUrl is not set in pipeline, try to build it from the ADO connection
                            // The AdoConnection entity has Organization property (e.g. "ThomaBravoTerraform") but we need the URL
                            if (!string.IsNullOrEmpty(adoConnection.OrganizationUrl))
                            {
                                organizationUrl = adoConnection.OrganizationUrl;
                                _logger.LogInformation("Resolved OrganizationUrl from ADO Connection: {OrganizationUrl}", organizationUrl);
                            }
                            else if (!string.IsNullOrEmpty(adoConnection.Organization))
                            {
                                organizationUrl = $"https://dev.azure.com/{adoConnection.Organization}";
                                _logger.LogInformation("Constructed OrganizationUrl from Organization name: {OrganizationUrl}", organizationUrl);
                            }
                        }

                        if (!string.IsNullOrEmpty(adoConnection.KeyVaultSecretName))
                        {
                            _logger.LogInformation("Retrieving PAT from KeyVault: {SecretName}", adoConnection.KeyVaultSecretName);
                            adoPat = await _keyVaultService.GetSecretAsync(adoConnection.KeyVaultSecretName);
                        }
                    }
                    else 
                    {
                        _logger.LogWarning("Linked ADO Connection {AdoConnectionId} not found.", pipeline.AdoConnectionId);
                    }
                }
                else 
                {
                    _logger.LogInformation("Pipeline {PipelineId} is NOT linked to an ADO Connection. Usage fallback logic.", pipeline.RowKey);
                }

                // Fallback for PAT
                if (string.IsNullOrEmpty(adoPat))
                {
                    _logger.LogInformation("PAT not found via Connection. Trying convention-based secrets...");
                    adoPat = await _keyVaultService.GetSecretAsync($"ado-pat-{message.TenantId}")
                        ?? await _keyVaultService.GetSecretAsync("ado-pat");
                }

                if (string.IsNullOrEmpty(organizationUrl))
                {
                    _logger.LogWarning("No Organization URL found for pipeline {PipelineId}", pipeline.RowKey);
                    await LogScanAsync(pipeline, "failed", "No Organization URL found", message.CorrelationId);
                    continue;
                }

                if (string.IsNullOrEmpty(adoPat))
                {
                    _logger.LogWarning("No ADO PAT found for pipeline {PipelineId}", pipeline.RowKey);
                    await LogScanAsync(pipeline, "failed", "No PAT found", message.CorrelationId);
                    continue;
                }

                // Resolve Project Name (fallback between ProjectName and Project properties since table might use "Project")
                var projectName = !string.IsNullOrEmpty(pipeline.ProjectName) ? pipeline.ProjectName : pipeline.Project;

                var patLog = !string.IsNullOrEmpty(adoPat) 
                    ? (adoPat.StartsWith("mock") ? "MOCK-TOKEN" : (adoPat.Length > 4 ? adoPat.Substring(0, 4) + "***" : "***"))
                    : "null";

                _logger.LogInformation("Attempting to scan pipeline. OrganizationUrl: {OrganizationUrl}, Project: {Project}, PipelineId: {PipelineId}. PAT: {PatLog}", 
                    organizationUrl, projectName, pipeline.PipelineId, patLog);

                // Step 1: Trigger the pipeline run in Azure DevOps
                _logger.LogInformation("Triggering pipeline run in Azure DevOps...");
                var triggerResult = await _adoService.TriggerPipelineRunAsync(
                    organizationUrl,
                    adoPat,
                    projectName,
                    pipeline.PipelineId,
                    parameters: null,
                    cancellationToken);

                if (!triggerResult.Success)
                {
                    _logger.LogWarning("Failed to trigger pipeline: {Error}", triggerResult.ErrorMessage);
                    await LogScanAsync(pipeline, "failed", $"Failed to trigger pipeline: {triggerResult.ErrorMessage}", message.CorrelationId);
                    continue;
                }

                _logger.LogInformation("Pipeline triggered successfully. RunId: {RunId}, URL: {RunUrl}", 
                    triggerResult.RunId, triggerResult.RunUrl);

                ReportProgress(progress, message, pipeline, "running_pipeline", 20,
                    $"Pipeline triggered. Waiting for completion (Run #{triggerResult.RunId})...");

                // Step 2: Wait for the pipeline to complete
                var maxWaitTime = TimeSpan.FromMinutes(30); // Max wait time
                var pollInterval = TimeSpan.FromSeconds(15);
                var startTime = DateTime.UtcNow;
                PipelineRunStatus? runStatus = null;

                while (DateTime.UtcNow - startTime < maxWaitTime)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await Task.Delay(pollInterval, cancellationToken);

                    runStatus = await _adoService.GetPipelineRunStatusAsync(
                        organizationUrl,
                        adoPat,
                        projectName,
                        triggerResult.RunId,
                        cancellationToken);

                    _logger.LogInformation("Pipeline run status: State={State}, Result={Result}", 
                        runStatus.State, runStatus.Result);

                    // Check if completed (state is "completed" or similar)
                    if (runStatus.State.Equals("Completed", StringComparison.OrdinalIgnoreCase) ||
                        runStatus.State.Equals("completed", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    // Update progress based on time elapsed
                    var elapsed = DateTime.UtcNow - startTime;
                    var progressPercent = Math.Min(35, 20 + (int)(elapsed.TotalSeconds / maxWaitTime.TotalSeconds * 15));
                    ReportProgress(progress, message, pipeline, "running_pipeline", progressPercent,
                        $"Pipeline running... (Run #{triggerResult.RunId})");
                }

                if (runStatus == null || !runStatus.State.Equals("Completed", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Pipeline run did not complete within timeout. State: {State}", runStatus?.State ?? "unknown");
                    await LogScanAsync(pipeline, "failed", "Pipeline run timed out", message.CorrelationId);
                    continue;
                }

                if (!runStatus.Result.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) &&
                    !runStatus.Result.Equals("succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Pipeline run failed with result: {Result}. Fetching logs to analyze the error...", runStatus.Result);
                    
                    // Even when the pipeline fails, fetch logs and analyze with AI to explain the error
                    ReportProgress(progress, message, pipeline, "fetching_error_logs", 40,
                        "Pipeline failed. Fetching logs to analyze the error...");

                    try
                    {
                        var errorLogs = await _adoService.GetPipelineRunLogsAsync(
                            organizationUrl,
                            adoPat,
                            projectName,
                            triggerResult.RunId,
                            cancellationToken);

                        _logger.LogInformation("Retrieved error logs from failed pipeline. Log size: {LogSize} chars", 
                            errorLogs.FullLog?.Length ?? 0);

                        // Use AI to analyze why the terraform plan failed
                        ReportProgress(progress, message, pipeline, "analyzing_error", 60,
                            "Analyzing terraform error with AI...");

                        var errorAnalysis = await _terraformPlanAnalyzer.AnalyzePlanOutputAsync(
                            errorLogs.FullLog,
                            pipeline.PipelineName,
                            cancellationToken);

                        _logger.LogInformation("AI error analysis completed. Summary: {Summary}", 
                            errorAnalysis.Summary ?? "No summary");

                        // Update pipeline entity with AI analysis of the failure
                        pipeline.LastScanAt = DateTimeOffset.UtcNow;
                        pipeline.LastScanStatus = "failed";
                        pipeline.LastScanError = $"Pipeline run failed: {runStatus.Result}";
                        pipeline.LastScanSummary = !string.IsNullOrEmpty(errorAnalysis.Summary) 
                            ? errorAnalysis.Summary 
                            : $"El pipeline de terraform falló con resultado: {runStatus.Result}";
                        await _storageService.SavePipelineAsync(pipeline);

                        // Save any error-related drift items identified by AI
                        if (errorAnalysis.DriftItems.Any())
                        {
                            var errorDate = DateTimeOffset.UtcNow.ToString("yyyyMMdd");
                            foreach (var drift in errorAnalysis.DriftItems)
                            {
                                await _storageService.SaveDriftRecordAsync(new DriftRecordEntity
                                {
                                    PartitionKey = errorDate,
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
                                    Status = "error",
                                    TenantId = message.TenantId,
                                    CorrelationId = message.CorrelationId,
                                    DetectedAt = DateTimeOffset.UtcNow
                                });
                            }
                            totalDrifts += errorAnalysis.DriftItems.Count;
                        }

                        await LogScanAsync(pipeline, "failed", 
                            $"Pipeline failed: {runStatus.Result}. AI Analysis: {errorAnalysis.Summary}", 
                            message.CorrelationId);

                        ReportProgress(progress, message, pipeline, "failed", 100,
                            $"Pipeline failed. {errorAnalysis.Summary ?? "See logs for details."}");
                    }
                    catch (Exception logEx)
                    {
                        _logger.LogError(logEx, "Failed to fetch or analyze error logs");
                        
                        pipeline.LastScanAt = DateTimeOffset.UtcNow;
                        pipeline.LastScanStatus = "failed";
                        pipeline.LastScanError = $"Pipeline run failed: {runStatus.Result}";
                        await _storageService.SavePipelineAsync(pipeline);
                        
                        await LogScanAsync(pipeline, "failed", $"Pipeline run failed: {runStatus.Result}", message.CorrelationId);
                        
                        ReportProgress(progress, message, pipeline, "failed", 100,
                            $"Pipeline run failed: {runStatus.Result}");
                    }
                    
                    continue;
                }

                _logger.LogInformation("Pipeline run completed successfully. Fetching logs...");

                // Step 3: Get the pipeline logs to extract Terraform plan output
                ReportProgress(progress, message, pipeline, "fetching_logs", 40,
                    "Fetching pipeline logs...");

                var pipelineLogs = await _adoService.GetPipelineRunLogsAsync(
                    organizationUrl,
                    adoPat,
                    projectName,
                    triggerResult.RunId,
                    cancellationToken);

                _logger.LogInformation("Retrieved {LogCount} log entries from pipeline run. Log size: {LogSize} chars", 
                    pipelineLogs.Logs.Count, pipelineLogs.FullLog?.Length ?? 0);

                // Step 4: Analyze the Terraform plan output using AI
                ReportProgress(progress, message, pipeline, "analyzing_with_ai", 50,
                    "Analyzing Terraform plan with AI...");

                _logger.LogInformation("Sending terraform plan output to AI analyzer...");

                var analysisResult = await _terraformPlanAnalyzer.AnalyzePlanOutputAsync(
                    pipelineLogs.FullLog,
                    pipeline.PipelineName,
                    cancellationToken);

                _logger.LogInformation("AI analysis completed. Success: {Success}, Drifts: {DriftCount}, Risk: {Risk}", 
                    analysisResult.Success, analysisResult.DriftItems.Count, analysisResult.OverallRisk);

                if (!string.IsNullOrEmpty(analysisResult.Summary))
                {
                    _logger.LogInformation("AI Summary: {Summary}", analysisResult.Summary);
                }

                var driftItems = analysisResult.DriftItems;

                ReportProgress(progress, message, pipeline, "saving_results", 70,
                    $"Found {driftItems.Count} drifts. Saving results...");

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

                // Update pipeline stats with AI analysis results
                pipeline.LastScanAt = DateTimeOffset.UtcNow;
                pipeline.LastScanStatus = "success";
                pipeline.LastScanError = null;
                pipeline.LastScanSummary = analysisResult.Summary;
                pipeline.DriftCount = driftItems.Count;
                await _storageService.SavePipelineAsync(pipeline);

                // Log scan
                await LogScanAsync(pipeline, "success", null, message.CorrelationId, driftItems.Count, 0);

                ReportProgress(progress, message, pipeline, "completed", 100,
                    analysisResult.Summary ?? $"Analysis complete. Found {driftItems.Count} drifts.");
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
        _logger.LogInformation("Found {Count} active pipelines before tenant filtering.", pipelines.Count());

        // Filter by tenant if specified
        if (!string.IsNullOrEmpty(message.TenantId))
        {
            pipelines = pipelines.Where(p => p.PartitionKey == message.TenantId || p.TenantId == message.TenantId);
            _logger.LogInformation("After tenant filtering ({TenantId}): {Count} pipelines remaining.", message.TenantId, pipelines.Count());
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

    /// <summary>
    /// Parse Terraform plan output from pipeline logs to extract drift information
    /// </summary>
    private List<DriftItem> ParseTerraformPlanFromLogs(string fullLog)
    {
        var driftItems = new List<DriftItem>();

        if (string.IsNullOrEmpty(fullLog))
        {
            _logger.LogWarning("No log content to parse for Terraform plan");
            return driftItems;
        }

        // Look for Terraform plan summary line: "Plan: X to add, Y to change, Z to destroy."
        var planSummaryMatch = System.Text.RegularExpressions.Regex.Match(
            fullLog,
            @"Plan:\s*(\d+)\s*to add,\s*(\d+)\s*to change,\s*(\d+)\s*to destroy",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (planSummaryMatch.Success)
        {
            var toAdd = int.Parse(planSummaryMatch.Groups[1].Value);
            var toChange = int.Parse(planSummaryMatch.Groups[2].Value);
            var toDestroy = int.Parse(planSummaryMatch.Groups[3].Value);

            _logger.LogInformation("Terraform plan summary: Add={Add}, Change={Change}, Destroy={Destroy}", 
                toAdd, toChange, toDestroy);
        }

        // Parse individual resource changes
        // Pattern: # azurerm_resource_type.resource_name will be created/updated/destroyed
        var resourceChangePattern = new System.Text.RegularExpressions.Regex(
            @"#\s*([\w_]+\.[\w_]+)\s+will be\s+(created|updated|destroyed|replaced)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (System.Text.RegularExpressions.Match match in resourceChangePattern.Matches(fullLog))
        {
            var resourceAddress = match.Groups[1].Value;
            var action = match.Groups[2].Value.ToLower();

            var parts = resourceAddress.Split('.');
            var resourceType = parts.Length > 0 ? parts[0] : resourceAddress;
            var resourceName = parts.Length > 1 ? parts[1] : resourceAddress;

            var severity = action switch
            {
                "destroyed" => "CRITICAL",
                "replaced" => "HIGH",
                "updated" => "MEDIUM",
                "created" => "LOW",
                _ => "INFO"
            };

            var description = action switch
            {
                "destroyed" => $"Resource {resourceName} will be DESTROYED",
                "replaced" => $"Resource {resourceName} will be REPLACED (destroy + create)",
                "updated" => $"Resource {resourceName} will be UPDATED in place",
                "created" => $"Resource {resourceName} will be CREATED",
                _ => $"Resource {resourceName} has pending changes"
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
                Description = description,
                Recommendation = action == "destroyed" 
                    ? "Review the plan carefully. This will permanently delete the resource."
                    : "Run terraform apply to reconcile the drift.",
                Category = severity == "CRITICAL" ? "security" : "configuration"
            });
        }

        // Also look for "~ " lines indicating attribute changes
        var attributeChangePattern = new System.Text.RegularExpressions.Regex(
            @"~\s+([\w_]+)\s+=\s+""([^""]*)""\s+->\s+""([^""]*)""",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        foreach (System.Text.RegularExpressions.Match match in attributeChangePattern.Matches(fullLog))
        {
            var property = match.Groups[1].Value;
            var oldValue = match.Groups[2].Value;
            var newValue = match.Groups[3].Value;

            // Only add if we have substantial info
            if (!string.IsNullOrEmpty(property))
            {
                driftItems.Add(new DriftItem
                {
                    ResourceId = "attribute_change",
                    ResourceType = "terraform_attribute",
                    ResourceName = property,
                    Property = property,
                    ExpectedValue = newValue,
                    ActualValue = oldValue,
                    Severity = "MEDIUM",
                    Description = $"Attribute {property} differs: '{oldValue}' -> '{newValue}'",
                    Recommendation = "Run terraform apply to update this attribute.",
                    Category = "configuration"
                });
            }
        }

        // Check for "No changes" message
        if (fullLog.Contains("No changes. Your infrastructure matches the configuration.", StringComparison.OrdinalIgnoreCase) ||
            fullLog.Contains("No changes. Infrastructure is up-to-date.", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Terraform plan indicates no drift - infrastructure is up-to-date");
        }

        return driftItems;
    }

    private class AzureCredentials
    {
        public string TenantId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
