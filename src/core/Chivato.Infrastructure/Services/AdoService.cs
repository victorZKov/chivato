using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Chivato.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Chivato.Infrastructure.Services;

public class AdoService : IAdoService
{
    private readonly HttpClient _httpClient;
    private readonly IKeyVaultService _keyVaultService;
    private readonly IAdoConnectionRepository _connectionRepository;
    private readonly ILogger<AdoService> _logger;

    public AdoService(
        HttpClient httpClient,
        IKeyVaultService keyVaultService,
        IAdoConnectionRepository connectionRepository,
        ILogger<AdoService> logger)
    {
        _httpClient = httpClient;
        _keyVaultService = keyVaultService;
        _connectionRepository = connectionRepository;
        _logger = logger;
    }

    public async Task<string?> GetFileContentAsync(
        string organization,
        string project,
        string repositoryId,
        string filePath,
        string branch,
        CancellationToken ct = default)
    {
        try
        {
            var pat = await GetPatForOrganizationAsync(organization, ct);
            if (string.IsNullOrEmpty(pat))
            {
                _logger.LogWarning("No PAT found for organization {Organization}", organization);
                return null;
            }

            var url = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/items" +
                      $"?path={Uri.EscapeDataString(filePath)}&versionDescriptor.version={Uri.EscapeDataString(branch)}" +
                      "&api-version=7.0";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                _logger.LogWarning("Failed to get file from ADO: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file {FilePath} from ADO", filePath);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(
        string organization,
        string project,
        string repositoryId,
        string path,
        string branch,
        CancellationToken ct = default)
    {
        try
        {
            var pat = await GetPatForOrganizationAsync(organization, ct);
            if (string.IsNullOrEmpty(pat))
            {
                _logger.LogWarning("No PAT found for organization {Organization}", organization);
                return Array.Empty<string>();
            }

            var url = $"https://dev.azure.com/{organization}/{project}/_apis/git/repositories/{repositoryId}/items" +
                      $"?scopePath={Uri.EscapeDataString(path)}&recursionLevel=Full" +
                      $"&versionDescriptor.version={Uri.EscapeDataString(branch)}&api-version=7.0";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to list files from ADO: {StatusCode}", response.StatusCode);
                return Array.Empty<string>();
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);

            var files = new List<string>();
            if (doc.RootElement.TryGetProperty("value", out var value))
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.TryGetProperty("path", out var pathProp) &&
                        item.TryGetProperty("gitObjectType", out var typeProp) &&
                        typeProp.GetString() == "blob")
                    {
                        files.Add(pathProp.GetString() ?? "");
                    }
                }
            }

            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing files from ADO path {Path}", path);
            throw;
        }
    }

    public async Task<bool> TestConnectionAsync(string organization, CancellationToken ct = default)
    {
        try
        {
            var pat = await GetPatForOrganizationAsync(organization, ct);
            if (string.IsNullOrEmpty(pat))
            {
                _logger.LogWarning("No PAT found for organization {Organization}", organization);
                return false;
            }

            var url = $"https://dev.azure.com/{organization}/_apis/projects?api-version=7.0";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

            var response = await _httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing ADO connection for {Organization}", organization);
            return false;
        }
    }

    private async Task<string?> GetPatForOrganizationAsync(string organization, CancellationToken ct)
    {
        // Note: In a real implementation, we'd need to get the connection by tenantId + organization
        // For now, we'll use a well-known secret name pattern
        var secretName = $"ado-pat-{organization.ToLowerInvariant()}";
        return await _keyVaultService.GetSecretAsync(secretName, ct);
    }
}
