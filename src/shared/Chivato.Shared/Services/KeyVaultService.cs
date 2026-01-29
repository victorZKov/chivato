using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Chivato.Shared.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _secretClient;

    public KeyVaultService(string keyVaultUrl)
    {
        _secretClient = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
    }

    public async Task<string?> GetSecretAsync(string secretName)
    {
        try
        {
            var secret = await _secretClient.GetSecretAsync(secretName);
            return secret.Value.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SetSecretAsync(string secretName, string value, DateTimeOffset? expiresOn = null)
    {
        var secret = new KeyVaultSecret(secretName, value);
        if (expiresOn.HasValue)
        {
            secret.Properties.ExpiresOn = expiresOn;
        }
        await _secretClient.SetSecretAsync(secret);
    }

    public async Task DeleteSecretAsync(string secretName)
    {
        await _secretClient.StartDeleteSecretAsync(secretName);
    }

    public async Task<DateTimeOffset?> GetSecretExpirationAsync(string secretName)
    {
        try
        {
            var secret = await _secretClient.GetSecretAsync(secretName);
            return secret.Value.Properties.ExpiresOn;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
