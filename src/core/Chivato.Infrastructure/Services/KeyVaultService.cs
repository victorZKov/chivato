using Azure;
using Azure.Security.KeyVault.Secrets;
using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chivato.Infrastructure.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _secretClient;
    private readonly ILogger<KeyVaultService> _logger;

    public KeyVaultService(SecretClient secretClient, ILogger<KeyVaultService> logger)
    {
        _secretClient = secretClient;
        _logger = logger;
    }

    public async Task<string?> GetSecretAsync(string secretName, CancellationToken ct = default)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: ct);
            return response.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret {SecretName} not found in Key Vault", secretName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving secret {SecretName} from Key Vault", secretName);
            throw;
        }
    }

    public async Task SetSecretAsync(string secretName, string value, DateTimeOffset? expiresOn = null, CancellationToken ct = default)
    {
        try
        {
            var secret = new KeyVaultSecret(secretName, value);

            if (expiresOn.HasValue)
            {
                secret.Properties.ExpiresOn = expiresOn.Value;
            }

            await _secretClient.SetSecretAsync(secret, ct);
            _logger.LogInformation("Secret {SecretName} set successfully", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting secret {SecretName} in Key Vault", secretName);
            throw;
        }
    }

    public async Task DeleteSecretAsync(string secretName, CancellationToken ct = default)
    {
        try
        {
            var operation = await _secretClient.StartDeleteSecretAsync(secretName, ct);
            await operation.WaitForCompletionAsync(ct);
            _logger.LogInformation("Secret {SecretName} deleted successfully", secretName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Secret {SecretName} not found, nothing to delete", secretName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting secret {SecretName} from Key Vault", secretName);
            throw;
        }
    }

    public async Task<DateTimeOffset?> GetSecretExpirationAsync(string secretName, CancellationToken ct = default)
    {
        try
        {
            var response = await _secretClient.GetSecretAsync(secretName, cancellationToken: ct);
            return response.Value.Properties.ExpiresOn;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiration for secret {SecretName}", secretName);
            throw;
        }
    }
}
