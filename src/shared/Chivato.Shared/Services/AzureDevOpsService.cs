using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Chivato.Shared.Models;
using System.Text.RegularExpressions;

namespace Chivato.Shared.Services;

/// <summary>
/// Azure DevOps service implementation for pipeline scanning
/// </summary>
public class AzureDevOpsService : IAdoService
{
    public async Task<bool> TestConnectionAsync(string organizationUrl, string pat)
    {
        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var projectClient = await connection.GetClientAsync<ProjectHttpClient>();
            var projects = await projectClient.GetProjects();

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<AdoProject>> GetProjectsAsync(string organizationUrl, string pat)
    {
        var credentials = new VssBasicCredential(string.Empty, pat);
        var connection = new VssConnection(new Uri(organizationUrl), credentials);

        var projectClient = await connection.GetClientAsync<ProjectHttpClient>();
        var projects = await projectClient.GetProjects();

        return projects.Select(p => new AdoProject
        {
            Id = p.Id.ToString(),
            Name = p.Name
        });
    }

    public async Task<IEnumerable<AdoPipelineInfo>> GetPipelinesAsync(string organizationUrl, string pat, string projectName)
    {
        var credentials = new VssBasicCredential(string.Empty, pat);
        var connection = new VssConnection(new Uri(organizationUrl), credentials);

        var buildClient = await connection.GetClientAsync<BuildHttpClient>();
        var definitions = await buildClient.GetDefinitionsAsync(projectName);

        return definitions.Select(d => new AdoPipelineInfo
        {
            Id = d.Id.ToString(),
            Name = d.Name,
            Folder = d.Path ?? "\\"
        });
    }

    public async Task<PipelineScanResult> ScanPipelineAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string pipelineId,
        CancellationToken cancellationToken = default)
    {
        var result = new PipelineScanResult
        {
            PipelineId = pipelineId
        };

        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var buildClient = await connection.GetClientAsync<BuildHttpClient>();

            // Get pipeline definition
            var definition = await buildClient.GetDefinitionAsync(
                projectName,
                int.Parse(pipelineId),
                cancellationToken: cancellationToken);

            result.PipelineName = definition.Name;

            // Try to get YAML content if it's a YAML pipeline
            if (definition.Process is YamlProcess yamlProcess)
            {
                result.YamlContent = await GetYamlContentAsync(
                    connection, projectName, yamlProcess.YamlFilename,
                    definition.Repository?.Name ?? "", cancellationToken);

                // Parse IaC references from YAML
                result.InfrastructureDefinitions = ParseInfrastructureDefinitions(result.YamlContent);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private async Task<string> GetYamlContentAsync(
        VssConnection connection,
        string projectName,
        string yamlFilePath,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use Git client to get file contents
            var gitClient = await connection.GetClientAsync<Microsoft.TeamFoundation.SourceControl.WebApi.GitHttpClient>();

            var repos = await gitClient.GetRepositoriesAsync(projectName, cancellationToken: cancellationToken);
            var repo = repos.FirstOrDefault(r => r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase))
                ?? repos.FirstOrDefault();

            if (repo != null)
            {
                // GetItemContentAsync returns Stream, need to read it
                var stream = await gitClient.GetItemContentAsync(
                    repo.Id,
                    yamlFilePath,
                    cancellationToken: cancellationToken);

                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync(cancellationToken);
            }
        }
        catch
        {
            // Continue without YAML content
        }

        return string.Empty;
    }

    private List<InfrastructureDefinition> ParseInfrastructureDefinitions(string yamlContent)
    {
        var definitions = new List<InfrastructureDefinition>();

        if (string.IsNullOrEmpty(yamlContent)) return definitions;

        // Look for ARM template references
        var armPattern = new Regex(@"template:\s*['""]?([^'""]+\.json)['""]?", RegexOptions.IgnoreCase);
        foreach (Match match in armPattern.Matches(yamlContent))
        {
            definitions.Add(new InfrastructureDefinition
            {
                Type = "ARM",
                FilePath = match.Groups[1].Value
            });
        }

        // Look for Bicep file references
        var bicepPattern = new Regex(@"template:\s*['""]?([^'""]+\.bicep)['""]?", RegexOptions.IgnoreCase);
        foreach (Match match in bicepPattern.Matches(yamlContent))
        {
            definitions.Add(new InfrastructureDefinition
            {
                Type = "Bicep",
                FilePath = match.Groups[1].Value
            });
        }

        // Look for Terraform references
        var tfPattern = new Regex(@"(?:workingDirectory|path):\s*['""]?([^'""]*terraform[^'""]*)['""]?", RegexOptions.IgnoreCase);
        foreach (Match match in tfPattern.Matches(yamlContent))
        {
            definitions.Add(new InfrastructureDefinition
            {
                Type = "Terraform",
                FilePath = match.Groups[1].Value
            });
        }

        return definitions;
    }
}
