namespace Chivato.IacWorker.Providers;

/// <summary>
/// Factory for getting the appropriate IaC provider
/// </summary>
public interface IIacProviderFactory
{
    /// <summary>
    /// Get provider by name (terraform, bicep, ansible, pulumi)
    /// </summary>
    IIacProvider? GetProvider(string providerName);

    /// <summary>
    /// Get all available providers
    /// </summary>
    IEnumerable<IIacProvider> GetAllProviders();

    /// <summary>
    /// Get available provider names
    /// </summary>
    IEnumerable<string> GetAvailableProviderNames();

    /// <summary>
    /// Detect the best provider for a directory based on file extensions
    /// </summary>
    Task<IIacProvider?> DetectProviderAsync(string directory, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of IaC provider factory
/// </summary>
public class IacProviderFactory : IIacProviderFactory
{
    private readonly IEnumerable<IIacProvider> _providers;
    private readonly ILogger<IacProviderFactory> _logger;

    public IacProviderFactory(IEnumerable<IIacProvider> providers, ILogger<IacProviderFactory> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    public IIacProvider? GetProvider(string providerName)
    {
        var provider = _providers.FirstOrDefault(p =>
            p.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));

        if (provider == null)
        {
            _logger.LogWarning("No IaC provider found for name: {ProviderName}", providerName);
        }

        return provider;
    }

    public IEnumerable<IIacProvider> GetAllProviders() => _providers;

    public IEnumerable<string> GetAvailableProviderNames() =>
        _providers.Select(p => p.Name);

    public async Task<IIacProvider?> DetectProviderAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Directory does not exist: {Directory}", directory);
            return null;
        }

        // Score each provider based on matching files
        var scores = new Dictionary<IIacProvider, int>();

        foreach (var provider in _providers)
        {
            var score = 0;
            foreach (var ext in provider.SupportedExtensions)
            {
                var files = Directory.GetFiles(directory, $"*{ext}", SearchOption.AllDirectories);
                score += files.Length;
            }

            if (score > 0)
            {
                // Check if provider is available
                var isAvailable = await provider.IsAvailableAsync(cancellationToken);
                if (isAvailable)
                {
                    scores[provider] = score;
                }
            }
        }

        var bestProvider = scores.OrderByDescending(kvp => kvp.Value).FirstOrDefault();

        if (bestProvider.Key != null)
        {
            _logger.LogInformation("Detected IaC provider: {Provider} (score: {Score})",
                bestProvider.Key.Name, bestProvider.Value);
            return bestProvider.Key;
        }

        _logger.LogWarning("No suitable IaC provider detected for directory: {Directory}", directory);
        return null;
    }
}
