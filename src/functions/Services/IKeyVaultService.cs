namespace Chivato.Functions.Services;

public interface IKeyVaultService
{
    Task<string?> GetSecretAsync(string secretName);
    Task SetSecretAsync(string secretName, string value, DateTimeOffset? expiresOn = null);
    Task DeleteSecretAsync(string secretName);
    Task<DateTimeOffset?> GetSecretExpirationAsync(string secretName);
}
