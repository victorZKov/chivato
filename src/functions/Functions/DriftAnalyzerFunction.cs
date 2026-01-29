using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Chivato.Functions.Services;
using Chivato.Functions.Models;

namespace Chivato.Functions.Functions;

public class DriftAnalyzerFunction
{
    private readonly ILogger<DriftAnalyzerFunction> _logger;
    private readonly IStorageService _storageService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly IAdoService _adoService;
    private readonly IAzureResourceService _azureResourceService;
    private readonly IAiAnalyzerService _aiAnalyzerService;
    private readonly IEmailService _emailService;

    public DriftAnalyzerFunction(
        ILogger<DriftAnalyzerFunction> logger,
        IStorageService storageService,
        IKeyVaultService keyVaultService,
        IAdoService adoService,
        IAzureResourceService azureResourceService,
        IAiAnalyzerService aiAnalyzerService,
        IEmailService emailService)
    {
        _logger = logger;
        _storageService = storageService;
        _keyVaultService = keyVaultService;
        _adoService = adoService;
        _azureResourceService = azureResourceService;
        _aiAnalyzerService = aiAnalyzerService;
        _emailService = emailService;
    }

    /// <summary>
    /// Timer trigger that runs drift analysis on configured pipelines
    /// Default: Every 24 hours at midnight UTC
    /// </summary>
    [Function("DriftAnalyzer")]
    public async Task Run([TimerTrigger("0 0 0 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Drift Analyzer started at: {time}", DateTime.UtcNow);

        try
        {
            // 1. Get active pipelines
            var pipelines = await _storageService.GetActivePipelinesAsync();
            _logger.LogInformation("Found {count} active pipelines to analyze", pipelines.Count());

            var allDriftRecords = new List<DriftRecordEntity>();

            foreach (var pipeline in pipelines)
            {
                try
                {
                    _logger.LogInformation("Analyzing pipeline: {name}", pipeline.PipelineName);

                    // 2. Get ADO connection credentials
                    var adoConnection = await _storageService.GetAdoConnectionAsync(pipeline.AdoConnectionId);
                    if (adoConnection == null)
                    {
                        _logger.LogWarning("ADO connection not found for pipeline {id}", pipeline.PipelineId);
                        continue;
                    }

                    var adoPat = await _keyVaultService.GetSecretAsync(adoConnection.KeyVaultSecretName);
                    if (string.IsNullOrEmpty(adoPat))
                    {
                        _logger.LogWarning("ADO PAT not found in Key Vault for connection {id}", adoConnection.RowKey);
                        continue;
                    }

                    // 3. Scan pipeline for IaC definitions
                    var pipelineResult = await _adoService.ScanPipelineAsync(
                        adoConnection.OrganizationUrl,
                        pipeline.ProjectName,
                        pipeline.PipelineId,
                        adoPat);

                    if (!pipelineResult.Success)
                    {
                        _logger.LogWarning("Failed to scan pipeline {id}: {error}", pipeline.PipelineId, pipelineResult.ErrorMessage);
                        continue;
                    }

                    // 4. Get Azure connection credentials
                    var azureConnection = await _storageService.GetAzureConnectionAsync(pipeline.AzureConnectionId);
                    if (azureConnection == null)
                    {
                        _logger.LogWarning("Azure connection not found for pipeline {id}", pipeline.PipelineId);
                        continue;
                    }

                    var clientSecret = await _keyVaultService.GetSecretAsync(azureConnection.KeyVaultSecretName);
                    if (string.IsNullOrEmpty(clientSecret))
                    {
                        _logger.LogWarning("Azure client secret not found in Key Vault for connection {id}", azureConnection.RowKey);
                        continue;
                    }

                    // 5. Get current Azure resources
                    var subscriptionIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(azureConnection.SubscriptionIds) ?? new List<string>();
                    var currentResources = new List<AzureResourceState>();

                    foreach (var subscriptionId in subscriptionIds)
                    {
                        var resourceGroups = await _azureResourceService.GetResourceGroupsAsync(
                            subscriptionId, azureConnection.TenantId, azureConnection.ClientId, clientSecret);

                        foreach (var rg in resourceGroups)
                        {
                            var resources = await _azureResourceService.GetResourcesAsync(
                                subscriptionId, rg, azureConnection.TenantId, azureConnection.ClientId, clientSecret);
                            currentResources.AddRange(resources);
                        }
                    }

                    // 6. Get AI connection
                    var aiConnection = await _storageService.GetActiveAiConnectionAsync();
                    if (aiConnection == null)
                    {
                        _logger.LogWarning("No active AI connection configured");
                        continue;
                    }

                    var aiApiKey = await _keyVaultService.GetSecretAsync(aiConnection.KeyVaultSecretName);
                    if (string.IsNullOrEmpty(aiApiKey))
                    {
                        _logger.LogWarning("AI API key not found in Key Vault");
                        continue;
                    }

                    // 7. Analyze drift with AI
                    var analysisResult = await _aiAnalyzerService.AnalyzeDriftAsync(
                        pipelineResult,
                        currentResources,
                        aiConnection.Endpoint,
                        aiConnection.DeploymentName,
                        aiApiKey);

                    _logger.LogInformation("Drift analysis complete for {name}: {count} items found, Risk: {risk}",
                        pipeline.PipelineName, analysisResult.DriftItems.Count, analysisResult.OverallRisk);

                    // 8. Save drift records
                    foreach (var driftItem in analysisResult.DriftItems)
                    {
                        var record = new DriftRecordEntity
                        {
                            PipelineId = pipeline.PipelineId,
                            ResourceId = driftItem.ResourceId,
                            ResourceType = driftItem.ResourceType,
                            ResourceName = driftItem.ResourceName,
                            Property = driftItem.Property,
                            ExpectedValue = driftItem.ExpectedValue,
                            ActualValue = driftItem.ActualValue,
                            DriftType = "PropertyMismatch",
                            Severity = driftItem.Severity,
                            Description = driftItem.Description,
                            Recommendation = driftItem.Recommendation,
                            Category = driftItem.Category,
                            DetectedAt = DateTimeOffset.UtcNow
                        };

                        await _storageService.SaveDriftRecordAsync(record);
                        allDriftRecords.Add(record);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing pipeline {id}", pipeline.PipelineId);
                }
            }

            // 9. Send email report if there are drift records
            if (allDriftRecords.Any())
            {
                await SendDriftReportAsync(allDriftRecords);
            }

            // 10. Cleanup old records
            var retentionDays = int.Parse(await _storageService.GetConfigValueAsync("retention_days") ?? "90");
            await _storageService.DeleteOldDriftRecordsAsync(retentionDays);

            _logger.LogInformation("Drift Analyzer completed at: {time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drift Analyzer failed");
            throw;
        }
    }

    private async Task SendDriftReportAsync(List<DriftRecordEntity> driftRecords)
    {
        try
        {
            var emailConfig = await _storageService.GetEmailServiceConfigAsync();
            if (emailConfig == null || !emailConfig.IsActive)
            {
                _logger.LogWarning("Email service not configured or inactive");
                return;
            }

            var connectionString = await _keyVaultService.GetSecretAsync(emailConfig.KeyVaultSecretName);
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Email connection string not found in Key Vault");
                return;
            }

            var recipients = await _storageService.GetEmailRecipientsAsync(activeOnly: true);
            var recipientEmails = recipients
                .Where(r => r.NotifyOn == "always" || r.NotifyOn == "drift_only")
                .Select(r => r.Email)
                .ToList();

            if (!recipientEmails.Any())
            {
                _logger.LogWarning("No email recipients configured");
                return;
            }

            await _emailService.SendDriftReportAsync(
                driftRecords,
                recipientEmails,
                connectionString,
                emailConfig.FromEmail,
                emailConfig.FromDisplayName);

            _logger.LogInformation("Drift report sent to {count} recipients", recipientEmails.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send drift report");
        }
    }
}
