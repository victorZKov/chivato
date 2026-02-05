using System.Diagnostics;
using Chivato.IacWorker.Providers;
using Chivato.IacWorker.Services;
using Chivato.Shared.Models;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;

namespace Chivato.IacWorker.Processors;

/// <summary>
/// Main processor for IaC drift analysis using pipeline triggers
/// </summary>
public class IacAnalysisProcessor : IIacAnalysisProcessor
{
    private readonly IAdoService _adoService;
    private readonly IStorageService _storageService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ISignalRService _signalRService;
    private readonly IEmailService _emailService;
    private readonly ITerraformPlanAnalyzer _terraformPlanAnalyzer;
    private readonly TerraformPlanParser _planParser;
    private readonly ILogger<IacAnalysisProcessor> _logger;

    // Polling settings
    private static readonly TimeSpan PipelineCheckInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PipelineTimeout = TimeSpan.FromMinutes(30);

    public IacAnalysisProcessor(
        IAdoService adoService,
        IStorageService storageService,
        IKeyVaultService keyVaultService,
        ISignalRService signalRService,
        IEmailService emailService,
        ITerraformPlanAnalyzer terraformPlanAnalyzer,
        ILogger<IacAnalysisProcessor> logger)
    {
        _adoService = adoService;
        _storageService = storageService;
        _keyVaultService = keyVaultService;
        _signalRService = signalRService;
        _emailService = emailService;
        _terraformPlanAnalyzer = terraformPlanAnalyzer;
        _planParser = new TerraformPlanParser();
        _logger = logger;
    }

    public async Task<IacAnalysisResultMessage> ProcessAsync(
        IacAnalysisMessage message,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new IacAnalysisResultMessage
        {
            CorrelationId = message.CorrelationId,
            TenantId = message.TenantId,
            PipelineId = message.PipelineId,
            IacType = message.IacType
        };

        try
        {
            _logger.LogInformation(
                "Starting IaC analysis for pipeline {PipelineId}, correlation {CorrelationId}",
                message.PipelineId, message.CorrelationId);

            // Get the pipeline info
            var pipeline = await _storageService.GetPipelineByIdAsync(message.PipelineId);
            if (pipeline == null)
            {
                throw new InvalidOperationException($"Pipeline {message.PipelineId} not found");
            }

            result.PipelineName = pipeline.PipelineName;

            // === STEP 0: Mark pipeline as Running and notify ===
            pipeline.LastScanStatus = "Running";
            pipeline.LastScanAt = DateTimeOffset.UtcNow;
            pipeline.LastScanError = null;
            await _storageService.SavePipelineAsync(pipeline);
            
            // Send SignalR notification for scan started (using analysisProgress)
            await _signalRService.SendAnalysisProgressAsync(message.TenantId, new AnalysisProgressEvent
            {
                CorrelationId = message.CorrelationId,
                PipelineId = message.PipelineId,
                PipelineName = pipeline.PipelineName,
                TenantId = message.TenantId,
                Stage = "starting",
                Progress = 0,
                Message = "Starting IaC analysis..."
            });
            
            _logger.LogInformation("Pipeline {PipelineId} marked as Running", message.PipelineId);

            // Report initial progress
            ReportProgress(progress, message, pipeline, "starting", 0, "Starting IaC analysis...");

            // Step 1: Get ADO credentials and organization URL
            var (pat, organizationUrl) = await GetAdoCredentialsAsync(message, pipeline);
            if (string.IsNullOrEmpty(pat))
            {
                throw new InvalidOperationException("No ADO PAT found for authentication");
            }
            if (string.IsNullOrEmpty(organizationUrl))
            {
                throw new InvalidOperationException("No ADO Organization URL found. Check the ADO connection configuration.");
            }

            // Update pipeline with resolved organization URL for subsequent calls
            pipeline.OrganizationUrl = organizationUrl;

            // Resolve ProjectName - use Project as fallback if ProjectName is empty
            var projectName = !string.IsNullOrEmpty(pipeline.ProjectName) 
                ? pipeline.ProjectName 
                : pipeline.Project;
            
            if (string.IsNullOrEmpty(projectName))
            {
                throw new InvalidOperationException("No Project name found. Check the pipeline configuration.");
            }
            
            // Ensure ProjectName is set for subsequent calls
            pipeline.ProjectName = projectName;

            _logger.LogInformation("Resolved pipeline config - Org: {OrgUrl}, Project: {Project}, PipelineId: {PipelineId}",
                organizationUrl, projectName, pipeline.PipelineId);

            // Step 2: Check for pending code changes
            ReportProgress(progress, message, pipeline, "checking_commits", 10, "Checking for pending code changes...");

            var pendingChanges = await CheckForPendingChangesAsync(pipeline, pat, cancellationToken);

            if (pendingChanges.HasPendingChanges)
            {
                _logger.LogInformation("Pipeline {PipelineId} has pending code changes. Last commit: {LastCommit}, Pipeline commit: {PipelineCommit}",
                    pipeline.PipelineId, pendingChanges.LatestCommitId, pendingChanges.LastPipelineCommitId);

                // Report drift: code is ahead of deployment
                var pendingDrift = new IacDrift
                {
                    ResourceAddress = "deployment",
                    ResourceType = "pending_deployment",
                    ResourceName = pipeline.PipelineName,
                    Action = "pending",
                    Severity = "HIGH",
                    Category = "deployment",
                    Description = $"Code has changed since last deployment. Latest commit: {pendingChanges.LatestCommitId?[..Math.Min(7, pendingChanges.LatestCommitId?.Length ?? 0)] ?? "unknown"}, Last deployed: {pendingChanges.LastPipelineCommitId?[..Math.Min(7, pendingChanges.LastPipelineCommitId?.Length ?? 0)] ?? "never"}",
                    Recommendation = "Run the pipeline to deploy the pending changes"
                };

                result.Status = "Completed";
                result.DriftItemCount = 1;
                result.OverallRisk = "HIGH";
                result.PlanSummary = new TerraformPlanSummary
                {
                    ToAdd = 0,
                    ToChange = 1,
                    ToDestroy = 0
                };

                await SaveDriftRecordAsync(message, pendingDrift, pipeline);
                
                // Update pipeline status
                pipeline.LastScanAt = DateTimeOffset.UtcNow;
                pipeline.LastScanStatus = "Success";
                pipeline.LastScanError = null;
                pipeline.DriftCount = 1;
                pipeline.LastScanSummary = "Pending deployment detected - code changes not yet deployed";
                await _storageService.SavePipelineAsync(pipeline);
                
                await LogScanAsync(pipeline, message, "Success", 1);
                
                // Send SignalR notification
                await _signalRService.SendToTenantAsync(message.TenantId, "scanCompleted", new
                {
                    CorrelationId = message.CorrelationId,
                    PipelineId = message.PipelineId,
                    PipelineName = pipeline.PipelineName,
                    Status = "Success",
                    DriftCount = 1,
                    Summary = pipeline.LastScanSummary,
                    CompletedAt = pipeline.LastScanAt
                });

                ReportProgress(progress, message, pipeline, "completed", 100,
                    "Pending deployment detected - code changes not yet deployed");

                stopwatch.Stop();
                result.ProcessingDuration = stopwatch.Elapsed;
                return result;
            }

            // Step 3: Trigger pipeline (without parameters to avoid validation errors)
            // Note: Some pipelines may not have the PLAN_ONLY parameter defined
            ReportProgress(progress, message, pipeline, "triggering_pipeline", 20, "Triggering pipeline for plan analysis...");

            // Try with parameters first, if it fails, retry without parameters
            var pipelineRun = await _adoService.TriggerPipelineRunAsync(
                pipeline.OrganizationUrl,
                pat,
                pipeline.ProjectName,
                pipeline.PipelineId,
                parameters: null,  // Don't pass parameters to avoid validation errors
                cancellationToken);

            if (!pipelineRun.Success)
            {
                throw new InvalidOperationException($"Failed to trigger pipeline: {pipelineRun.ErrorMessage}");
            }

            _logger.LogInformation("Triggered pipeline run {RunId} for pipeline {PipelineId}",
                pipelineRun.RunId, pipeline.PipelineId);

            // Step 4: Wait for pipeline completion
            ReportProgress(progress, message, pipeline, "waiting_pipeline", 30, "Waiting for pipeline to complete...");

            var runStatus = await WaitForPipelineCompletionAsync(
                pipeline, pat, pipelineRun.RunId, progress, message, cancellationToken);

            if (runStatus.Result != "Succeeded" && runStatus.Result != "succeeded")
            {
                _logger.LogWarning("Pipeline run {RunId} finished with result: {Result}",
                    pipelineRun.RunId, runStatus.Result);

                // Pipeline failed - analyze the error with AI
                ReportProgress(progress, message, pipeline, "analyzing_failure", 70, "Analyzing pipeline failure with AI...");
                
                var errorLogs = await _adoService.GetPipelineRunLogsAsync(
                    pipeline.OrganizationUrl,
                    pat,
                    pipeline.ProjectName,
                    pipelineRun.RunId,
                    cancellationToken);
                
                // Use AI to analyze the failed pipeline output
                _logger.LogInformation("Starting AI analysis of failed pipeline for {PipelineId}", message.PipelineId);
                
                var aiErrorAnalysis = await _terraformPlanAnalyzer.AnalyzePlanOutputAsync(
                    errorLogs.FullLog,
                    pipeline.PipelineName,
                    cancellationToken);
                
                // Convert AI results to drift records
                var allDriftRecords = new List<DriftRecordEntity>();
                
                foreach (var driftItem in aiErrorAnalysis.DriftItems)
                {
                    var drift = new IacDrift
                    {
                        ResourceAddress = driftItem.ResourceId,
                        ResourceType = driftItem.ResourceType,
                        ResourceName = driftItem.ResourceName,
                        Action = MapSeverityToAction(driftItem.Severity),
                        Severity = driftItem.Severity,
                        Category = driftItem.Category,
                        Description = driftItem.Description,
                        Recommendation = driftItem.Recommendation,
                        PropertyChanges = new List<IacPropertyChange>
                        {
                            new()
                            {
                                PropertyPath = driftItem.Property,
                                ExpectedValue = driftItem.ExpectedValue,
                                ActualValue = driftItem.ActualValue
                            }
                        }
                    };
                    
                    var record = await SaveDriftRecordAsync(message, drift, pipeline);
                    allDriftRecords.Add(record);
                }
                
                // If AI didn't return any drift items, create a generic failure record
                if (!allDriftRecords.Any())
                {
                    var errorContext = ExtractErrorContext(errorLogs.FullLog);
                    var errorSummary = ExtractErrorSummary(errorContext);
                    
                    var failureDrift = new IacDrift
                    {
                        ResourceAddress = "pipeline",
                        ResourceType = "pipeline_failure",
                        ResourceName = pipeline.PipelineName,
                        Action = "error",
                        Severity = "HIGH",
                        Category = "pipeline",
                        Description = $"Pipeline run failed with result: {runStatus.Result}. {errorSummary}",
                        Recommendation = errorContext,
                        PropertyChanges = new List<IacPropertyChange>
                        {
                            new()
                            {
                                PropertyPath = "pipeline.error",
                                ExpectedValue = "Pipeline should complete successfully",
                                ActualValue = errorSummary
                            }
                        }
                    };
                    
                    var failureRecord = await SaveDriftRecordAsync(message, failureDrift, pipeline);
                    allDriftRecords.Add(failureRecord);
                }
                
                // Update pipeline status - mark as completed with issues found
                pipeline.LastScanAt = DateTimeOffset.UtcNow;
                pipeline.LastScanStatus = "CompletedWithErrors";
                pipeline.LastScanError = $"Pipeline failed: {runStatus.Result}";
                pipeline.DriftCount = allDriftRecords.Count;
                pipeline.LastScanSummary = aiErrorAnalysis.Summary ?? $"Pipeline failed. Found {allDriftRecords.Count} issue(s).";
                await _storageService.SavePipelineAsync(pipeline);
                
                // Log the scan as completed (not failed - the analysis completed successfully)
                await LogScanAsync(pipeline, message, "CompletedWithErrors", allDriftRecords.Count, $"Pipeline failed: {runStatus.Result}");
                
                // Send SignalR notification - analysis completed (with issues)
                await _signalRService.SendAnalysisCompletedAsync(message.TenantId, new AnalysisCompletedEvent
                {
                    CorrelationId = message.CorrelationId,
                    PipelineId = message.PipelineId,
                    PipelineName = pipeline.PipelineName,
                    TenantId = message.TenantId,
                    Status = "CompletedWithErrors",
                    DriftCount = allDriftRecords.Count,
                    OverallRisk = aiErrorAnalysis.OverallRisk,
                    Summary = new AnalysisSummary
                    {
                        TotalDrifts = allDriftRecords.Count,
                        Critical = allDriftRecords.Count(r => r.Severity == "CRITICAL"),
                        High = allDriftRecords.Count(r => r.Severity == "HIGH"),
                        Medium = allDriftRecords.Count(r => r.Severity == "MEDIUM"),
                        Low = allDriftRecords.Count(r => r.Severity == "LOW" || r.Severity == "INFO")
                    }
                });
                
                stopwatch.Stop();
                
                result.Status = "CompletedWithErrors";
                result.DriftItemCount = allDriftRecords.Count;
                result.OverallRisk = aiErrorAnalysis.OverallRisk;
                result.ProcessingDuration = stopwatch.Elapsed;
                result.ErrorMessage = $"Pipeline failed: {runStatus.Result}";
                
                ReportProgress(progress, message, pipeline, "completed", 100, 
                    $"Analysis completed. Pipeline failed with {allDriftRecords.Count} issue(s) detected.");
                
                _logger.LogInformation(
                    "IaC analysis completed for pipeline {PipelineId} (pipeline failed). Found {IssueCount} issues. Duration: {Duration}ms",
                    message.PipelineId, allDriftRecords.Count, stopwatch.ElapsedMilliseconds);
                
                return result;
            }

            // Step 5: Get pipeline logs
            ReportProgress(progress, message, pipeline, "fetching_logs", 70, "Fetching pipeline logs...");

            var logs = await _adoService.GetPipelineRunLogsAsync(
                pipeline.OrganizationUrl,
                pat,
                pipeline.ProjectName,
                pipelineRun.RunId,
                cancellationToken);

            // Step 6: Analyze terraform plan with AI
            ReportProgress(progress, message, pipeline, "analyzing_plan", 80, "Analyzing terraform plan with AI...");

            _logger.LogInformation("Starting AI analysis of terraform plan for pipeline {PipelineId}", message.PipelineId);
            
            var aiAnalysis = await _terraformPlanAnalyzer.AnalyzePlanOutputAsync(
                logs.FullLog,
                pipeline.PipelineName,
                cancellationToken);

            if (!aiAnalysis.Success && string.IsNullOrEmpty(aiAnalysis.ErrorMessage))
            {
                _logger.LogWarning("AI analysis returned unsuccessful without error message");
            }
            else if (!aiAnalysis.Success)
            {
                _logger.LogWarning("AI analysis failed: {Error}", aiAnalysis.ErrorMessage);
            }
            else
            {
                _logger.LogInformation(
                    "AI analysis complete. Found {DriftCount} items. Risk: {Risk}", 
                    aiAnalysis.DriftItems.Count, 
                    aiAnalysis.OverallRisk);
            }

            // Convert AI DriftItems to IacDrift for storage
            var iacDrifts = aiAnalysis.DriftItems.Select(d => new IacDrift
            {
                ResourceAddress = d.ResourceId,
                ResourceType = d.ResourceType,
                ResourceName = d.ResourceName,
                Action = MapSeverityToAction(d.Severity),
                Severity = d.Severity,
                Category = d.Category,
                Description = d.Description,
                Recommendation = d.Recommendation,
                PropertyChanges = new List<IacPropertyChange>
                {
                    new()
                    {
                        PropertyPath = d.Property,
                        ExpectedValue = d.ExpectedValue,
                        ActualValue = d.ActualValue
                    }
                }
            }).ToList();

            // Step 7: Save drift records
            var driftRecords = new List<DriftRecordEntity>();

            foreach (var drift in iacDrifts)
            {
                var record = await SaveDriftRecordAsync(message, drift, pipeline);
                driftRecords.Add(record);
            }

            // Send Notifications (Email & SignalR)
            if (driftRecords.Any())
            {
                await SendNotificationsAsync(pipeline, driftRecords);
            }

            // Update pipeline stats - mark as Success
            pipeline.LastScanAt = DateTimeOffset.UtcNow;
            pipeline.LastScanStatus = "Success";
            pipeline.LastScanError = null;
            pipeline.DriftCount = iacDrifts.Count;
            pipeline.LastScanSummary = aiAnalysis.DriftItems.Any() 
                ? aiAnalysis.Summary
                : "No drift detected - infrastructure matches IaC";
            await _storageService.SavePipelineAsync(pipeline);

            // Log the scan
            await LogScanAsync(pipeline, message, "Success", iacDrifts.Count);

            // Send SignalR notification for scan completed
            await _signalRService.SendAnalysisCompletedAsync(message.TenantId, new AnalysisCompletedEvent
            {
                CorrelationId = message.CorrelationId,
                PipelineId = message.PipelineId,
                PipelineName = pipeline.PipelineName,
                TenantId = message.TenantId,
                Status = "Success",
                DriftCount = iacDrifts.Count,
                OverallRisk = aiAnalysis.OverallRisk,
                Summary = new AnalysisSummary
                {
                    TotalDrifts = iacDrifts.Count,
                    Critical = iacDrifts.Count(d => d.Severity == "CRITICAL"),
                    High = iacDrifts.Count(d => d.Severity == "HIGH"),
                    Medium = iacDrifts.Count(d => d.Severity == "MEDIUM"),
                    Low = iacDrifts.Count(d => d.Severity == "LOW" || d.Severity == "INFO")
                }
            });

            stopwatch.Stop();

            // Build final result
            result.Status = "Completed";
            result.DriftItemCount = iacDrifts.Count;
            result.OverallRisk = aiAnalysis.OverallRisk;
            result.ProcessingDuration = stopwatch.Elapsed;

            // Extract plan summary from AI analysis if available
            if (!string.IsNullOrEmpty(aiAnalysis.PlanSummary))
            {
                var summaryMatch = System.Text.RegularExpressions.Regex.Match(
                    aiAnalysis.PlanSummary,
                    @"(\d+)\s*to add.*?(\d+)\s*to change.*?(\d+)\s*to destroy",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (summaryMatch.Success)
                {
                    result.PlanSummary = new TerraformPlanSummary
                    {
                        ToAdd = int.TryParse(summaryMatch.Groups[1].Value, out var add) ? add : 0,
                        ToChange = int.TryParse(summaryMatch.Groups[2].Value, out var change) ? change : 0,
                        ToDestroy = int.TryParse(summaryMatch.Groups[3].Value, out var destroy) ? destroy : 0
                    };
                }
            }

            var statusMessage = aiAnalysis.DriftItems.Any()
                ? $"Analysis complete. Found {result.DriftItemCount} drift items"
                : "Analysis complete. No drift detected - infrastructure matches IaC";

            ReportProgress(progress, message, pipeline, "completed", 100, statusMessage);

            _logger.LogInformation(
                "IaC analysis completed for pipeline {PipelineId}. Found {DriftCount} drifts. Duration: {Duration}ms",
                message.PipelineId, result.DriftItemCount, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (OperationCanceledException)
        {
            result.Status = "Cancelled";
            result.ErrorMessage = "Analysis was cancelled";
            
            // Update pipeline status even on cancellation
            try
            {
                var pipeline = await _storageService.GetPipelineByIdAsync(message.PipelineId);
                if (pipeline != null)
                {
                    pipeline.LastScanAt = DateTimeOffset.UtcNow;
                    pipeline.LastScanStatus = "Cancelled";
                    pipeline.LastScanError = "Analysis was cancelled";
                    await _storageService.SavePipelineAsync(pipeline);
                    await LogScanAsync(pipeline, message, "Failed", 0, "Analysis was cancelled");
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogWarning(innerEx, "Failed to update pipeline status after cancellation");
            }
            
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IaC analysis failed for pipeline {PipelineId}", message.PipelineId);
            stopwatch.Stop();

            result.Status = "Failed";
            result.ErrorMessage = ex.Message;
            result.ProcessingDuration = stopwatch.Elapsed;

            // Update pipeline status and log failed scan
            try
            {
                var pipeline = await _storageService.GetPipelineByIdAsync(message.PipelineId);
                if (pipeline != null)
                {
                    pipeline.LastScanAt = DateTimeOffset.UtcNow;
                    pipeline.LastScanStatus = "Failed";
                    pipeline.LastScanError = ex.Message;
                    pipeline.LastScanSummary = null;
                    await _storageService.SavePipelineAsync(pipeline);
                    
                    // Send SignalR notification for scan failed
                    await _signalRService.SendAnalysisFailedAsync(message.TenantId, new AnalysisFailedEvent
                    {
                        CorrelationId = message.CorrelationId,
                        PipelineId = message.PipelineId,
                        PipelineName = pipeline.PipelineName,
                        TenantId = message.TenantId,
                        Error = ex.Message
                    });
                }
                
                await LogScanAsync(pipeline, message, "Failed", 0, ex.Message);
            }
            catch (Exception innerEx)
            {
                _logger.LogWarning(innerEx, "Failed to update pipeline status after error");
            }

            return result;
        }
    }

    private async Task<(string? Pat, string? OrganizationUrl)> GetAdoCredentialsAsync(IacAnalysisMessage message, PipelineEntity pipeline)
    {
        string? pat = null;
        string? organizationUrl = pipeline.OrganizationUrl;

        // Try connection-specific PAT first
        if (!string.IsNullOrEmpty(pipeline.AdoConnectionId))
        {
            var connection = await _storageService.GetAdoConnectionAsync(pipeline.AdoConnectionId);
            if (connection != null)
            {
                // Get organization URL from connection if not set on pipeline
                if (string.IsNullOrEmpty(organizationUrl))
                {
                    organizationUrl = connection.OrganizationUrl;
                    // If still empty, construct from Organization name
                    if (string.IsNullOrEmpty(organizationUrl) && !string.IsNullOrEmpty(connection.Organization))
                    {
                        organizationUrl = $"https://dev.azure.com/{connection.Organization}";
                    }
                }

                if (!string.IsNullOrEmpty(connection.KeyVaultSecretName))
                {
                    pat = await _keyVaultService.GetSecretAsync(connection.KeyVaultSecretName);
                }
            }
        }

        // Try from message if still no PAT
        if (string.IsNullOrEmpty(pat) && !string.IsNullOrEmpty(message.Repository.AdoConnectionId))
        {
            var connection = await _storageService.GetAdoConnectionAsync(message.Repository.AdoConnectionId);
            if (connection != null)
            {
                if (string.IsNullOrEmpty(organizationUrl))
                {
                    organizationUrl = connection.OrganizationUrl;
                    if (string.IsNullOrEmpty(organizationUrl) && !string.IsNullOrEmpty(connection.Organization))
                    {
                        organizationUrl = $"https://dev.azure.com/{connection.Organization}";
                    }
                }

                if (!string.IsNullOrEmpty(connection.KeyVaultSecretName))
                {
                    pat = await _keyVaultService.GetSecretAsync(connection.KeyVaultSecretName);
                }
            }
        }

        // Fall back to tenant-specific or global PAT
        if (string.IsNullOrEmpty(pat))
        {
            pat = await _keyVaultService.GetSecretAsync($"ado-pat-{message.TenantId}");
            if (string.IsNullOrEmpty(pat))
            {
                pat = await _keyVaultService.GetSecretAsync("ado-pat");
            }
        }

        return (pat, organizationUrl);
    }

    private async Task<PendingChangesResult> CheckForPendingChangesAsync(
        PipelineEntity pipeline,
        string pat,
        CancellationToken cancellationToken)
    {
        var result = new PendingChangesResult();

        try
        {
            // Get the latest commit from the repository
            var latestCommit = await _adoService.GetLatestCommitAsync(
                pipeline.OrganizationUrl,
                pat,
                pipeline.ProjectName,
                pipeline.RepositoryName ?? pipeline.ProjectName, // Use repo name or project name as default
                pipeline.Branch ?? "main",
                cancellationToken);

            result.LatestCommitId = latestCommit?.CommitId;

            // Get the last pipeline run
            var lastRun = await _adoService.GetLastPipelineRunAsync(
                pipeline.OrganizationUrl,
                pat,
                pipeline.ProjectName,
                pipeline.PipelineId,
                cancellationToken);

            result.LastPipelineCommitId = lastRun?.SourceCommitId;
            result.LastPipelineRunAt = lastRun?.FinishedAt;

            // Compare commits
            if (!string.IsNullOrEmpty(result.LatestCommitId) &&
                !string.IsNullOrEmpty(result.LastPipelineCommitId))
            {
                result.HasPendingChanges = !result.LatestCommitId.Equals(
                    result.LastPipelineCommitId,
                    StringComparison.OrdinalIgnoreCase);
            }
            else if (!string.IsNullOrEmpty(result.LatestCommitId) &&
                     string.IsNullOrEmpty(result.LastPipelineCommitId))
            {
                // Pipeline never ran
                result.HasPendingChanges = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for pending changes, assuming none");
            result.HasPendingChanges = false;
        }

        return result;
    }

    private async Task<PipelineRunStatus> WaitForPipelineCompletionAsync(
        PipelineEntity pipeline,
        string pat,
        int runId,
        IProgress<IacProgressEvent>? progress,
        IacAnalysisMessage message,
        CancellationToken cancellationToken)
    {
        var startTime = DateTimeOffset.UtcNow;
        var progressPercent = 30;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = await _adoService.GetPipelineRunStatusAsync(
                pipeline.OrganizationUrl,
                pat,
                pipeline.ProjectName,
                runId,
                cancellationToken);

            // Check if completed
            if (status.State.Equals("completed", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }

            // Check timeout
            if (DateTimeOffset.UtcNow - startTime > PipelineTimeout)
            {
                throw new TimeoutException($"Pipeline run {runId} did not complete within {PipelineTimeout.TotalMinutes} minutes");
            }

            // Update progress
            progressPercent = Math.Min(progressPercent + 5, 80);
            progress?.Report(new IacProgressEvent
            {
                CorrelationId = message.CorrelationId,
                TenantId = message.TenantId,
                PipelineId = message.PipelineId,
                PipelineName = pipeline.PipelineName,
                IacType = message.IacType,
                Stage = "waiting_pipeline",
                Progress = progressPercent,
                Message = $"Pipeline run {runId} status: {status.State}"
            });

            await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
        }
    }

    private async Task<DriftRecordEntity> SaveDriftRecordAsync(
        IacAnalysisMessage message,
        IacDrift drift,
        PipelineEntity pipeline)
    {
        var record = new DriftRecordEntity
        {
            RowKey = Guid.NewGuid().ToString(),
            PipelineId = message.PipelineId,
            PipelineName = pipeline.PipelineName,
            ResourceId = drift.ResourceAddress,
            ResourceType = drift.ResourceType,
            ResourceName = drift.ResourceName,
            Property = drift.PropertyChanges.FirstOrDefault()?.PropertyPath ?? drift.Action,
            ExpectedValue = drift.PropertyChanges.FirstOrDefault()?.ExpectedValue ?? "as defined in IaC",
            ActualValue = drift.PropertyChanges.FirstOrDefault()?.ActualValue ?? "current Azure state",
            Severity = drift.Severity,
            Description = drift.Description,
            Recommendation = drift.Recommendation,
            Category = drift.Category,
            Status = "new",
            TenantId = message.TenantId,
            CorrelationId = message.CorrelationId,
            DetectedAt = DateTimeOffset.UtcNow
        };

        await _storageService.SaveDriftRecordAsync(record);
        return record;
    }

    private async Task SendNotificationsAsync(PipelineEntity pipeline, List<DriftRecordEntity> driftRecords)
    {
        try 
        {
            // 1. Send SignalR update
            await _signalRService.SendDriftDetectedAsync(pipeline.TenantId, new DriftAnalysisResult
            {
                DriftItems = driftRecords.Select(d => new DriftItem
                {
                    ResourceId = d.ResourceId,
                    ResourceName = d.ResourceName,
                    ResourceType = d.ResourceType,
                    Severity = d.Severity,
                    // Map other fields as needed
                }).ToList(),
                OverallRisk = driftRecords.Any(d => d.Severity == "CRITICAL") ? "CRITICAL" : "HIGH"
            });

            // 2. Send Emails
            var emailConfig = await _storageService.GetEmailServiceConfigAsync();
            if (emailConfig != null && emailConfig.IsActive && !string.IsNullOrEmpty(emailConfig.KeyVaultSecretName))
            {
                 var connectionString = await _keyVaultService.GetSecretAsync(emailConfig.KeyVaultSecretName);
                 var recipients = await _storageService.GetEmailRecipientsAsync(activeOnly: true);
                 var recipientEmails = recipients
                    .Where(r => (r.NotifyOn == "always" || r.NotifyOn == "drift_only") && r.TenantId == pipeline.TenantId)
                    .Select(r => r.Email)
                    .ToList();

                 if (recipientEmails.Any() && !string.IsNullOrEmpty(connectionString))
                 {
                     await _emailService.SendDriftReportAsync(
                         recipientEmails, 
                         pipeline.PipelineName, 
                         driftRecords, 
                         connectionString, 
                         emailConfig.FromEmail);
                 }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notifications for pipeline {PipelineId}", pipeline.PipelineId);
        }
    }

    private async Task LogScanAsync(
        PipelineEntity? pipeline,
        IacAnalysisMessage message,
        string status,
        int driftCount,
        string? error = null)
    {
        var today = DateTimeOffset.UtcNow.ToString("yyyyMMdd");

        await _storageService.SaveScanLogAsync(new ScanLogEntity
        {
            PartitionKey = today,
            RowKey = Guid.NewGuid().ToString(),
            PipelineId = message.PipelineId,
            PipelineName = pipeline?.PipelineName ?? "Unknown",
            TenantId = message.TenantId,
            Status = status,
            DriftCount = driftCount,
            ErrorMessage = error,
            CorrelationId = message.CorrelationId,
            StartedAt = DateTimeOffset.UtcNow,
            TriggeredBy = message.InitiatedBy ?? "iac-worker"
        });
    }

    private string CalculateOverallRisk(TerraformPlanResult result)
    {
        if (!result.HasChanges) return "NONE";
        if (result.Drifts.Any(d => d.Severity == "CRITICAL")) return "CRITICAL";
        if (result.Drifts.Any(d => d.Severity == "HIGH")) return "HIGH";
        if (result.Drifts.Any(d => d.Severity == "MEDIUM")) return "MEDIUM";
        if (result.Drifts.Any(d => d.Severity == "LOW")) return "LOW";
        return "NONE";
    }

    private void ReportProgress(
        IProgress<IacProgressEvent>? progress,
        IacAnalysisMessage message,
        PipelineEntity pipeline,
        string stage,
        int percent,
        string msg)
    {
        progress?.Report(new IacProgressEvent
        {
            CorrelationId = message.CorrelationId,
            PipelineId = message.PipelineId,
            PipelineName = pipeline.PipelineName,
            TenantId = message.TenantId,
            IacType = message.IacType,
            Stage = stage,
            Progress = percent,
            Message = msg
        });
    }

    /// <summary>
    /// Extracts meaningful error context from pipeline logs
    /// </summary>
    private string ExtractErrorContext(string? fullLog)
    {
        if (string.IsNullOrEmpty(fullLog))
            return "No error details available. Check the pipeline in Azure DevOps.";

        var lines = fullLog.Split('\n');

        static bool IsNoiseLine(string line)
        {
            var trimmed = line.Trim();
            return string.IsNullOrWhiteSpace(trimmed)
                || trimmed.StartsWith("{")
                || trimmed.StartsWith("}")
                || trimmed.StartsWith("#")
                || trimmed.Contains("Write-Host", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("Get-Command", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("##[section]", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("##[command]", StringComparison.OrdinalIgnoreCase);
        }

        static List<string> CollectContext(string[] allLines, Func<string, bool> isMatch)
        {
            for (int i = 0; i < allLines.Length; i++)
            {
                var line = allLines[i].Trim();
                if (!isMatch(line))
                    continue;

                var block = new List<string>();
                for (int j = i; j < Math.Min(i + 8, allLines.Length); j++)
                {
                    var candidate = allLines[j].Trim();
                    if (!string.IsNullOrWhiteSpace(candidate))
                        block.Add(candidate);
                }
                return block;
            }

            return new List<string>();
        }

        var errorLines = CollectContext(lines, line => line.Contains("##[error]", StringComparison.OrdinalIgnoreCase));

        if (!errorLines.Any())
        {
            errorLines = CollectContext(lines, line =>
                line.Contains("Error:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Exception", StringComparison.OrdinalIgnoreCase)
                || line.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("denied", StringComparison.OrdinalIgnoreCase)
                || line.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                || line.Contains("not found", StringComparison.OrdinalIgnoreCase)
                || line.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        }

        var cleanedLines = errorLines.Where(line => !IsNoiseLine(line)).Take(12).ToList();

        if (cleanedLines.Any())
        {
            var errorContext = string.Join("\n", cleanedLines);
            return errorContext.Length > 1500
                ? errorContext.Substring(0, 1500) + "..."
                : errorContext;
        }

        // If no specific error found, return last part of log
        var lastLines = lines.Where(l => !IsNoiseLine(l)).TakeLast(12);
        var fallback = string.Join("\n", lastLines);
        return fallback.Length > 1500
            ? fallback.Substring(0, 1500) + "..."
            : fallback;
    }

    private string ExtractErrorSummary(string? errorContext)
    {
        if (string.IsNullOrWhiteSpace(errorContext))
            return "No error details available.";

        var firstLine = errorContext.Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))
            ?? "No error details available.";

        return firstLine.Length > 160
            ? firstLine.Substring(0, 160) + "..."
            : firstLine;
    }

    /// <summary>
    /// Map severity level to action type for drift records
    /// </summary>
    private static string MapSeverityToAction(string severity) => severity.ToUpperInvariant() switch
    {
        "CRITICAL" => "destroy",
        "HIGH" => "replace",
        "MEDIUM" => "update",
        "LOW" => "create",
        _ => "update"
    };
}

/// <summary>
/// Result of checking for pending code changes
/// </summary>
internal class PendingChangesResult
{
    public bool HasPendingChanges { get; set; }
    public string? LatestCommitId { get; set; }
    public string? LastPipelineCommitId { get; set; }
    public DateTimeOffset? LastPipelineRunAt { get; set; }
}
