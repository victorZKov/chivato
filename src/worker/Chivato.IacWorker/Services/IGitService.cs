using Chivato.Shared.Models.Messages;

namespace Chivato.IacWorker.Services;

/// <summary>
/// Service for Git operations (clone, checkout, etc.)
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Clone a repository from Azure DevOps
    /// </summary>
    /// <param name="repoInfo">Repository information</param>
    /// <param name="pat">Personal Access Token for authentication</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Local path where the repository was cloned</returns>
    Task<GitCloneResult> CloneRepositoryAsync(
        RepositoryInfo repoInfo,
        string pat,
        IProgress<IacProgressEvent>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up a cloned repository directory
    /// </summary>
    Task CleanupAsync(string localPath);

    /// <summary>
    /// Get the working directory for a correlation ID
    /// </summary>
    string GetWorkingDirectory(string correlationId);
}

/// <summary>
/// Result of a git clone operation
/// </summary>
public class GitCloneResult
{
    public bool Success { get; set; }
    public string LocalPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string CommitHash { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
}
