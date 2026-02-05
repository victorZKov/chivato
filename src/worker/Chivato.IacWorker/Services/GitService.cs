using System.Diagnostics;
using System.Text;
using Chivato.Shared.Models.Messages;
using Chivato.Shared.Services;
using CliWrap;
using CliWrap.Buffered;

namespace Chivato.IacWorker.Services;

/// <summary>
/// Git service implementation for cloning Azure DevOps repositories
/// </summary>
public class GitService : IGitService
{
    private readonly string _baseWorkingDirectory;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ILogger<GitService> _logger;

    public GitService(
        string baseWorkingDirectory,
        IKeyVaultService keyVaultService,
        ILogger<GitService> logger)
    {
        _baseWorkingDirectory = baseWorkingDirectory;
        _keyVaultService = keyVaultService;
        _logger = logger;
    }

    public string GetWorkingDirectory(string correlationId)
    {
        return Path.Combine(_baseWorkingDirectory, correlationId);
    }

    public async Task<GitCloneResult> CloneRepositoryAsync(
        RepositoryInfo repoInfo,
        string pat,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString();
        var localPath = GetWorkingDirectory(correlationId);

        try
        {
            _logger.LogInformation("Cloning repository {RepoName} from {Organization}/{Project}",
                repoInfo.RepositoryName, repoInfo.OrganizationUrl, repoInfo.ProjectName);

            progress?.Report(new IacProgressEvent
            {
                Stage = "cloning",
                Progress = 5,
                Message = $"Cloning repository {repoInfo.RepositoryName}..."
            });

            // Ensure the directory exists
            Directory.CreateDirectory(localPath);

            // Build the clone URL with embedded PAT for authentication
            // Format: https://{pat}@dev.azure.com/{org}/{project}/_git/{repo}
            var cloneUrl = BuildCloneUrl(repoInfo, pat);

            // Clone the repository
            var cloneResult = await Cli.Wrap("git")
                .WithArguments(new[]
                {
                    "clone",
                    "--depth", "1",
                    "--single-branch",
                    "--branch", repoInfo.Branch,
                    cloneUrl,
                    localPath
                })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            if (cloneResult.ExitCode != 0)
            {
                var errorMessage = SanitizeGitOutput(cloneResult.StandardError);
                _logger.LogError("Git clone failed: {Error}", errorMessage);

                return new GitCloneResult
                {
                    Success = false,
                    ErrorMessage = errorMessage,
                    Duration = stopwatch.Elapsed
                };
            }

            _logger.LogInformation("Repository cloned successfully to {Path}", localPath);

            progress?.Report(new IacProgressEvent
            {
                Stage = "cloning",
                Progress = 15,
                Message = "Repository cloned successfully"
            });

            // Get the current commit hash
            var commitHash = await GetCommitHashAsync(localPath, cancellationToken);

            // If IacPath is specified, verify it exists
            var iacFullPath = localPath;
            if (!string.IsNullOrEmpty(repoInfo.IacPath))
            {
                iacFullPath = Path.Combine(localPath, repoInfo.IacPath);
                if (!Directory.Exists(iacFullPath))
                {
                    _logger.LogError("IaC path does not exist: {IacPath}", iacFullPath);
                    return new GitCloneResult
                    {
                        Success = false,
                        ErrorMessage = $"IaC path '{repoInfo.IacPath}' does not exist in the repository",
                        Duration = stopwatch.Elapsed
                    };
                }
            }

            stopwatch.Stop();

            return new GitCloneResult
            {
                Success = true,
                LocalPath = iacFullPath,
                Branch = repoInfo.Branch,
                CommitHash = commitHash,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clone repository {RepoName}", repoInfo.RepositoryName);

            // Clean up on failure
            await CleanupAsync(localPath);

            return new GitCloneResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task CleanupAsync(string localPath)
    {
        if (string.IsNullOrEmpty(localPath) || !Directory.Exists(localPath))
            return;

        try
        {
            // Remove read-only attributes from .git directory (common issue on Windows)
            await Task.Run(() =>
            {
                var gitDir = Path.Combine(localPath, ".git");
                if (Directory.Exists(gitDir))
                {
                    RemoveReadOnlyAttributes(gitDir);
                }

                Directory.Delete(localPath, recursive: true);
            });

            _logger.LogInformation("Cleaned up working directory: {Path}", localPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup directory: {Path}", localPath);
        }
    }

    private string BuildCloneUrl(RepositoryInfo repoInfo, string pat)
    {
        // Parse organization URL to extract org name
        // Expected format: https://dev.azure.com/orgname or https://orgname.visualstudio.com
        var uri = new Uri(repoInfo.OrganizationUrl);

        string cloneUrl;
        if (uri.Host.EndsWith("visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            // Old-style URL: https://orgname.visualstudio.com
            var orgName = uri.Host.Split('.')[0];
            cloneUrl = $"https://{pat}@{orgName}.visualstudio.com/{repoInfo.ProjectName}/_git/{repoInfo.RepositoryName}";
        }
        else
        {
            // New-style URL: https://dev.azure.com/orgname
            var orgName = uri.AbsolutePath.Trim('/').Split('/')[0];
            cloneUrl = $"https://{pat}@dev.azure.com/{orgName}/{repoInfo.ProjectName}/_git/{repoInfo.RepositoryName}";
        }

        return cloneUrl;
    }

    private async Task<string> GetCommitHashAsync(string repoPath, CancellationToken cancellationToken)
    {
        try
        {
            var result = await Cli.Wrap("git")
                .WithWorkingDirectory(repoPath)
                .WithArguments(new[] { "rev-parse", "HEAD" })
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            return result.StandardOutput.Trim();
        }
        catch
        {
            return "unknown";
        }
    }

    private string SanitizeGitOutput(string output)
    {
        // Remove PAT from any error messages
        if (string.IsNullOrEmpty(output))
            return output;

        // Pattern to match PAT in URLs
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            output,
            @"https://[^@]+@",
            "https://***@");

        return sanitized;
    }

    private void RemoveReadOnlyAttributes(string directory)
    {
        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(file);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
        }

        foreach (var dir in Directory.GetDirectories(directory, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(dir);
            if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(dir, attributes & ~FileAttributes.ReadOnly);
            }
        }
    }
}
