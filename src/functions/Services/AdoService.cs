using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Chivato.Functions.Models;
using System.Text.RegularExpressions;

namespace Chivato.Functions.Services;

public class AdoService : IAdoService
{
    private readonly ILogger<AdoService> _logger;

    public AdoService(ILogger<AdoService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> TestConnectionAsync(string organizationUrl, string pat)
    {
        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var projectClient = connection.GetClient<ProjectHttpClient>();
            var projects = await projectClient.GetProjects();

            _logger.LogInformation("ADO connection test successful. Found {Count} projects", projects.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ADO connection test failed for {OrganizationUrl}", organizationUrl);
            return false;
        }
    }

    public async Task<IEnumerable<string>> GetProjectsAsync(string organizationUrl, string pat)
    {
        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var projectClient = connection.GetClient<ProjectHttpClient>();
            var projects = await projectClient.GetProjects();

            return projects.Select(p => p.Name).OrderBy(n => n);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get projects from {OrganizationUrl}", organizationUrl);
            throw;
        }
    }

    public async Task<IEnumerable<(string Id, string Name)>> GetPipelinesAsync(
        string organizationUrl, string projectName, string pat)
    {
        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var buildClient = connection.GetClient<BuildHttpClient>();
            var definitions = await buildClient.GetDefinitionsAsync(project: projectName);

            return definitions
                .Select(d => (Id: d.Id.ToString(), Name: d.Name))
                .OrderBy(d => d.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pipelines from {Project}", projectName);
            throw;
        }
    }

    public async Task<PipelineScanResult> ScanPipelineAsync(
        string organizationUrl, string projectName, string pipelineId, string pat)
    {
        var result = new PipelineScanResult
        {
            PipelineId = pipelineId
        };

        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var buildClient = connection.GetClient<BuildHttpClient>();

            // Get pipeline definition
            var definitionId = int.Parse(pipelineId);
            var definition = await buildClient.GetDefinitionAsync(projectName, definitionId);

            result.PipelineName = definition.Name;

            // Get YAML content if it's a YAML pipeline
            if (definition.Process is YamlProcess yamlProcess)
            {
                result.YamlContent = await GetYamlContentAsync(
                    organizationUrl, projectName, pat,
                    definition.Repository?.Id,
                    yamlProcess.YamlFilename,
                    definition.Repository?.DefaultBranch ?? "main");

                // Extract infrastructure definitions from YAML
                result.InfrastructureDefinitions = ExtractInfrastructureDefinitions(result.YamlContent);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan pipeline {PipelineId} in {Project}", pipelineId, projectName);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<string> GetYamlContentAsync(
        string organizationUrl, string projectName, string pat,
        string? repositoryId, string yamlFilePath, string branch)
    {
        if (string.IsNullOrEmpty(repositoryId) || string.IsNullOrEmpty(yamlFilePath))
        {
            return string.Empty;
        }

        try
        {
            // Use REST API to get file content from repository
            using var httpClient = new HttpClient();
            var encodedPat = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{pat}"));
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", encodedPat);

            var cleanBranch = branch.Replace("refs/heads/", "");
            var encodedPath = Uri.EscapeDataString(yamlFilePath);

            var url = $"{organizationUrl}/{projectName}/_apis/git/repositories/{repositoryId}/items" +
                     $"?path={encodedPath}&versionDescriptor.version={cleanBranch}&api-version=7.0";

            var response = await httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            _logger.LogWarning("Failed to get YAML content: {StatusCode}", response.StatusCode);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting YAML content from repository");
            return string.Empty;
        }
    }

    private List<InfrastructureDefinition> ExtractInfrastructureDefinitions(string yamlContent)
    {
        var definitions = new List<InfrastructureDefinition>();

        if (string.IsNullOrEmpty(yamlContent))
            return definitions;

        // Look for ARM template references
        var armPatterns = new[]
        {
            @"template:\s*['""]?([^'""]+\.json)['""]?",
            @"AzureResourceManagerTemplateDeployment.*templateLocation.*linkedArtifact.*csmFile:\s*['""]?([^'""]+\.json)['""]?"
        };

        foreach (var pattern in armPatterns)
        {
            var matches = Regex.Matches(yamlContent, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match match in matches)
            {
                definitions.Add(new InfrastructureDefinition
                {
                    Type = "ARM",
                    FilePath = match.Groups[1].Value,
                    Content = string.Empty // Will be populated in a full scan
                });
            }
        }

        // Look for Bicep references
        var bicepPatterns = new[]
        {
            @"template:\s*['""]?([^'""]+\.bicep)['""]?",
            @"csmFile:\s*['""]?([^'""]+\.bicep)['""]?"
        };

        foreach (var pattern in bicepPatterns)
        {
            var matches = Regex.Matches(yamlContent, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                definitions.Add(new InfrastructureDefinition
                {
                    Type = "Bicep",
                    FilePath = match.Groups[1].Value,
                    Content = string.Empty
                });
            }
        }

        // Look for Terraform references
        var terraformPatterns = new[]
        {
            @"TerraformTaskV\d+@\d+",
            @"workingDirectory:\s*['""]?([^'""]*terraform[^'""]*)['""]?"
        };

        foreach (var pattern in terraformPatterns)
        {
            var matches = Regex.Matches(yamlContent, pattern, RegexOptions.IgnoreCase);
            if (matches.Count > 0)
            {
                definitions.Add(new InfrastructureDefinition
                {
                    Type = "Terraform",
                    FilePath = matches.Count > 1 ? matches[1].Groups[1].Value : "terraform/",
                    Content = string.Empty
                });
            }
        }

        // Look for inline ARM/Bicep in YAML (inlineScript with az deployment)
        var inlineDeploymentPattern = @"az\s+deployment\s+(group|sub|mg|tenant)\s+create.*--template-file\s+([^\s]+)";
        var inlineMatches = Regex.Matches(yamlContent, inlineDeploymentPattern, RegexOptions.IgnoreCase);
        foreach (Match match in inlineMatches)
        {
            var templateFile = match.Groups[2].Value.Trim('\'', '"');
            var type = templateFile.EndsWith(".bicep") ? "Bicep" : "ARM";
            definitions.Add(new InfrastructureDefinition
            {
                Type = type,
                FilePath = templateFile,
                Content = string.Empty
            });
        }

        return definitions.DistinctBy(d => d.FilePath).ToList();
    }
}
