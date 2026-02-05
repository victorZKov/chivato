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

    public async Task<PipelineRunResult> TriggerPipelineRunAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string pipelineId,
        Dictionary<string, string>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var buildClient = await connection.GetClientAsync<BuildHttpClient>();

            // Create build parameters
            var build = new Build
            {
                Definition = new DefinitionReference
                {
                    Id = int.Parse(pipelineId)
                }
            };

            // Add parameters if provided
            if (parameters != null && parameters.Count > 0)
            {
                var paramJson = System.Text.Json.JsonSerializer.Serialize(parameters);
                build.Parameters = paramJson;
            }

            // Queue the build
            var queuedBuild = await buildClient.QueueBuildAsync(build, projectName, cancellationToken: cancellationToken);

            return new PipelineRunResult
            {
                Success = true,
                RunId = queuedBuild.Id,
                RunUrl = queuedBuild.Url ?? $"{organizationUrl}/{projectName}/_build/results?buildId={queuedBuild.Id}"
            };
        }
        catch (Exception ex)
        {
            return new PipelineRunResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<PipelineRunStatus> GetPipelineRunStatusAsync(
        string organizationUrl,
        string pat,
        string projectName,
        int runId,
        CancellationToken cancellationToken = default)
    {
        var credentials = new VssBasicCredential(string.Empty, pat);
        var connection = new VssConnection(new Uri(organizationUrl), credentials);

        var buildClient = await connection.GetClientAsync<BuildHttpClient>();
        var build = await buildClient.GetBuildAsync(projectName, runId, cancellationToken: cancellationToken);

        return new PipelineRunStatus
        {
            RunId = build.Id,
            State = build.Status?.ToString() ?? "unknown",
            Result = build.Result?.ToString() ?? "",
            SourceCommitId = build.SourceVersion,
            StartedAt = build.StartTime,
            FinishedAt = build.FinishTime
        };
    }

    public async Task<PipelineRunLogs> GetPipelineRunLogsAsync(
        string organizationUrl,
        string pat,
        string projectName,
        int runId,
        CancellationToken cancellationToken = default)
    {
        var result = new PipelineRunLogs { RunId = runId };

        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var buildClient = await connection.GetClientAsync<BuildHttpClient>();

            // Get build logs
            var logs = await buildClient.GetBuildLogsAsync(projectName, runId, cancellationToken: cancellationToken);

            var fullLog = new System.Text.StringBuilder();

            foreach (var logRef in logs)
            {
                try
                {
                    // Get log content using stream
                    var logStream = await buildClient.GetBuildLogAsync(
                        projectName, runId, logRef.Id, cancellationToken: cancellationToken);

                    using var reader = new StreamReader(logStream);
                    var content = await reader.ReadToEndAsync(cancellationToken);

                    result.Logs.Add(new PipelineLogEntry
                    {
                        LogId = logRef.Id,
                        Content = content
                    });

                    fullLog.AppendLine(content);
                }
                catch
                {
                    // Skip logs that can't be read
                }
            }

            result.FullLog = fullLog.ToString();
        }
        catch (Exception ex)
        {
            result.FullLog = $"Error getting logs: {ex.Message}";
        }

        return result;
    }

    public async Task<PipelineRunInfo?> GetLastPipelineRunAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string pipelineId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var buildClient = await connection.GetClientAsync<BuildHttpClient>();

            // Get the most recent build for this pipeline
            var builds = await buildClient.GetBuildsAsync(
                projectName,
                definitions: new[] { int.Parse(pipelineId) },
                top: 1,
                cancellationToken: cancellationToken);

            var lastBuild = builds.FirstOrDefault();
            if (lastBuild == null) return null;

            return new PipelineRunInfo
            {
                RunId = lastBuild.Id,
                PipelineId = pipelineId,
                State = lastBuild.Status?.ToString() ?? "unknown",
                Result = lastBuild.Result?.ToString() ?? "",
                SourceCommitId = lastBuild.SourceVersion,
                SourceBranch = lastBuild.SourceBranch,
                CreatedAt = lastBuild.QueueTime,
                FinishedAt = lastBuild.FinishTime
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<GitCommitInfo?> GetLatestCommitAsync(
        string organizationUrl,
        string pat,
        string projectName,
        string repositoryName,
        string branch = "main",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var credentials = new VssBasicCredential(string.Empty, pat);
            var connection = new VssConnection(new Uri(organizationUrl), credentials);

            var gitClient = await connection.GetClientAsync<Microsoft.TeamFoundation.SourceControl.WebApi.GitHttpClient>();

            // Get repository
            var repos = await gitClient.GetRepositoriesAsync(projectName, cancellationToken: cancellationToken);
            var repo = repos.FirstOrDefault(r => r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));

            if (repo == null) return null;

            // Get the latest commits from the branch
            var commits = await gitClient.GetCommitsAsync(
                repo.Id,
                new Microsoft.TeamFoundation.SourceControl.WebApi.GitQueryCommitsCriteria
                {
                    ItemVersion = new Microsoft.TeamFoundation.SourceControl.WebApi.GitVersionDescriptor
                    {
                        Version = branch,
                        VersionType = Microsoft.TeamFoundation.SourceControl.WebApi.GitVersionType.Branch
                    },
                    Top = 1
                },
                cancellationToken: cancellationToken);

            var latestCommit = commits.FirstOrDefault();
            if (latestCommit == null) return null;

            return new GitCommitInfo
            {
                CommitId = latestCommit.CommitId,
                Message = latestCommit.Comment ?? "",
                Author = latestCommit.Author?.Name ?? "",
                CommittedAt = latestCommit.Author?.Date ?? DateTimeOffset.MinValue
            };
        }
        catch
        {
            return null;
        }
    }
}
