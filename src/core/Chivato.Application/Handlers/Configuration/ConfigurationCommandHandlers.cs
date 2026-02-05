using Chivato.Application.Commands.Configuration;
using Chivato.Application.Common;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using Chivato.Domain.ValueObjects;
using MediatR;
using DomainConfig = Chivato.Domain.Entities.Configuration;

namespace Chivato.Application.Handlers.Configuration;

public class UpdateTimerHandler : IRequestHandler<UpdateTimerCommand, CommandResult>
{
    private readonly IConfigurationRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateTimerHandler(IConfigurationRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<CommandResult> Handle(UpdateTimerCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new CommandResult(false, "Only admins can modify settings");

        if (request.IntervalHours < 1 || request.IntervalHours > 168)
            return new CommandResult(false, "Interval must be between 1 and 168 hours");

        try
        {
            var config = DomainConfig.Create(
                _currentUser.TenantId,
                DomainConfig.Keys.ScanIntervalHours,
                request.IntervalHours.ToString(),
                ConfigurationCategory.Scanning
            );

            await _repository.SetAsync(config, ct);
            return new CommandResult(true);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }
}

public class UpdateSettingsHandler : IRequestHandler<UpdateSettingsCommand, CommandResult>
{
    private readonly IConfigurationRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdateSettingsHandler(IConfigurationRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<CommandResult> Handle(UpdateSettingsCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new CommandResult(false, "Only admins can modify settings");

        try
        {
            var tenantId = _currentUser.TenantId;

            await _repository.SetAsync(DomainConfig.Create(
                tenantId, DomainConfig.Keys.EmailNotificationsEnabled,
                request.EmailNotificationsEnabled.ToString(), ConfigurationCategory.Notifications), ct);

            await _repository.SetAsync(DomainConfig.Create(
                tenantId, DomainConfig.Keys.MinimumSeverityForAlert,
                request.MinimumSeverityForAlert, ConfigurationCategory.Notifications), ct);

            await _repository.SetAsync(DomainConfig.Create(
                tenantId, DomainConfig.Keys.MaxConcurrentScans,
                request.MaxConcurrentScans.ToString(), ConfigurationCategory.Scanning), ct);

            await _repository.SetAsync(DomainConfig.Create(
                tenantId, DomainConfig.Keys.RetentionDays,
                request.RetentionDays.ToString(), ConfigurationCategory.General), ct);

            return new CommandResult(true);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }
}

public class SaveAzureConnectionHandler : IRequestHandler<SaveAzureConnectionCommand, SaveConnectionResult>
{
    private readonly IAzureConnectionRepository _repository;
    private readonly IKeyVaultService _keyVault;
    private readonly ICurrentUser _currentUser;

    public SaveAzureConnectionHandler(
        IAzureConnectionRepository repository,
        IKeyVaultService keyVault,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _keyVault = keyVault;
        _currentUser = currentUser;
    }

    public async Task<SaveConnectionResult> Handle(SaveAzureConnectionCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new SaveConnectionResult(string.Empty, false, "Only admins can modify connections");

        try
        {
            var tenantId = _currentUser.TenantId;
            var secretName = $"azure-sp-{tenantId}-{request.SubscriptionId}";

            // Store secret in Key Vault
            await _keyVault.SetSecretAsync(secretName, request.ClientSecret, null, ct);

            AzureConnection connection;

            if (string.IsNullOrEmpty(request.Id))
            {
                // Create new
                connection = AzureConnection.Create(
                    tenantId, request.Name, request.TenantId,
                    request.SubscriptionId, request.ClientId, secretName, request.IsDefault
                );
                await _repository.AddAsync(connection, ct);
            }
            else
            {
                // Update existing
                connection = await _repository.GetByIdAsync(tenantId, request.Id, ct)
                    ?? throw new InvalidOperationException("Connection not found");

                connection.Update(request.Name, request.TenantId, request.SubscriptionId, request.ClientId, secretName);

                if (request.IsDefault)
                    connection.MarkAsDefault();

                await _repository.UpdateAsync(connection, ct);
            }

            return new SaveConnectionResult(connection.Id, true);
        }
        catch (Exception ex)
        {
            return new SaveConnectionResult(string.Empty, false, ex.Message);
        }
    }
}

public class TestAzureConnectionHandler : IRequestHandler<TestAzureConnectionCommand, TestConnectionResult>
{
    private readonly IAzureConnectionRepository _repository;
    private readonly IKeyVaultService _keyVault;
    private readonly ICurrentUser _currentUser;

    public TestAzureConnectionHandler(
        IAzureConnectionRepository repository,
        IKeyVaultService keyVault,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _keyVault = keyVault;
        _currentUser = currentUser;
    }

    public async Task<TestConnectionResult> Handle(TestAzureConnectionCommand request, CancellationToken ct)
    {
        try
        {
            var connection = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, ct);
            if (connection == null)
                return new TestConnectionResult(false, "Error", "Connection not found");

            // Get the client secret from Key Vault
            var clientSecret = await _keyVault.GetSecretAsync(connection.ClientSecretKeyVaultKey, ct);
            if (string.IsNullOrEmpty(clientSecret))
                return new TestConnectionResult(false, "Error", "Client secret not found in Key Vault");

            // Create credential with the connection's Service Principal
            var credential = new Azure.Identity.ClientSecretCredential(
                connection.AzureTenantId,
                connection.ClientId,
                clientSecret);

            // Create ArmClient with the specific credential
            var armClient = new Azure.ResourceManager.ArmClient(credential);

            // Test by getting the subscription - this validates the credentials
            var subscription = armClient.GetSubscriptionResource(
                new Azure.Core.ResourceIdentifier($"/subscriptions/{connection.SubscriptionId}"));
            var subscriptionData = await subscription.GetAsync(ct);

            // If we get here, the connection is valid
            connection.RecordTestSuccess();
            await _repository.UpdateAsync(connection, ct);

            return new TestConnectionResult(true, "Connected");
        }
        catch (Exception ex)
        {
            // Record failure
            var connection = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, ct);
            if (connection != null)
            {
                connection.RecordTestFailure(ex.Message);
                await _repository.UpdateAsync(connection, ct);
            }

            return new TestConnectionResult(false, "Error", ex.Message);
        }
    }
}

public class AddEmailRecipientHandler : IRequestHandler<AddEmailRecipientCommand, SaveConnectionResult>
{
    private readonly IEmailRecipientRepository _repository;
    private readonly ICurrentUser _currentUser;

    public AddEmailRecipientHandler(IEmailRecipientRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<SaveConnectionResult> Handle(AddEmailRecipientCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new SaveConnectionResult(string.Empty, false, "Only admins can add recipients");

        try
        {
            // Check if email already exists
            var existing = await _repository.GetByEmailAsync(_currentUser.TenantId, request.Email, ct);
            if (existing != null)
                return new SaveConnectionResult(string.Empty, false, "Email already registered");

            var preferences = new NotificationPreferences
            {
                MinimumSeverity = SeverityExtensions.FromString(request.MinimumSeverity),
                NotifyOnScanComplete = request.NotifyOnScanComplete,
                NotifyOnNewDrift = request.NotifyOnNewDrift
            };

            var recipient = EmailRecipient.Create(
                _currentUser.TenantId,
                request.Email,
                request.Name,
                preferences
            );

            await _repository.AddAsync(recipient, ct);

            return new SaveConnectionResult(recipient.Id, true);
        }
        catch (Exception ex)
        {
            return new SaveConnectionResult(string.Empty, false, ex.Message);
        }
    }
}

public class RemoveEmailRecipientHandler : IRequestHandler<RemoveEmailRecipientCommand, CommandResult>
{
    private readonly IEmailRecipientRepository _repository;
    private readonly ICurrentUser _currentUser;

    public RemoveEmailRecipientHandler(IEmailRecipientRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<CommandResult> Handle(RemoveEmailRecipientCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new CommandResult(false, "Only admins can remove recipients");

        try
        {
            await _repository.DeleteAsync(_currentUser.TenantId, request.Id, ct);
            return new CommandResult(true);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }
}

public class DeleteAzureConnectionHandler : IRequestHandler<DeleteAzureConnectionCommand, CommandResult>
{
    private readonly IAzureConnectionRepository _repository;
    private readonly IKeyVaultService _keyVault;
    private readonly ICurrentUser _currentUser;

    public DeleteAzureConnectionHandler(
        IAzureConnectionRepository repository,
        IKeyVaultService keyVault,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _keyVault = keyVault;
        _currentUser = currentUser;
    }

    public async Task<CommandResult> Handle(DeleteAzureConnectionCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new CommandResult(false, "Only admins can delete connections");

        try
        {
            var connection = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, ct);
            if (connection == null)
                return new CommandResult(false, "Connection not found");

            // Delete secret from Key Vault
            await _keyVault.DeleteSecretAsync(connection.ClientSecretKeyVaultKey, ct);

            // Delete the connection
            await _repository.DeleteAsync(_currentUser.TenantId, request.Id, ct);
            return new CommandResult(true);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }
}

public class SaveAdoConnectionHandler : IRequestHandler<SaveAdoConnectionCommand, SaveConnectionResult>
{
    private readonly IAdoConnectionRepository _repository;
    private readonly IKeyVaultService _keyVault;
    private readonly ICurrentUser _currentUser;

    public SaveAdoConnectionHandler(
        IAdoConnectionRepository repository,
        IKeyVaultService keyVault,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _keyVault = keyVault;
        _currentUser = currentUser;
    }

    public async Task<SaveConnectionResult> Handle(SaveAdoConnectionCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new SaveConnectionResult(string.Empty, false, "Only admins can modify connections");

        try
        {
            var tenantId = _currentUser.TenantId;
            var secretName = $"ado-pat-{tenantId}-{request.Organization}";

            // Store PAT in Key Vault
            await _keyVault.SetSecretAsync(secretName, request.PatToken, null, ct);

            AdoConnection connection;

            if (string.IsNullOrEmpty(request.Id))
            {
                // Create new
                connection = AdoConnection.Create(
                    tenantId, request.Name, request.Organization,
                    request.Project, secretName, request.IsDefault
                );
                await _repository.AddAsync(connection, ct);
            }
            else
            {
                // Update existing
                connection = await _repository.GetByIdAsync(tenantId, request.Id, ct)
                    ?? throw new InvalidOperationException("Connection not found");

                connection.Update(request.Name, request.Organization, request.Project, secretName);

                if (request.IsDefault)
                    connection.MarkAsDefault();

                await _repository.UpdateAsync(connection, ct);
            }

            return new SaveConnectionResult(connection.Id, true);
        }
        catch (Exception ex)
        {
            return new SaveConnectionResult(string.Empty, false, ex.Message);
        }
    }
}

public class TestAdoConnectionHandler : IRequestHandler<TestAdoConnectionCommand, TestConnectionResult>
{
    private readonly IAdoConnectionRepository _repository;
    private readonly IKeyVaultService _keyVault;
    private readonly ICurrentUser _currentUser;

    public TestAdoConnectionHandler(
        IAdoConnectionRepository repository,
        IKeyVaultService keyVault,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _keyVault = keyVault;
        _currentUser = currentUser;
    }

    public async Task<TestConnectionResult> Handle(TestAdoConnectionCommand request, CancellationToken ct)
    {
        try
        {
            var connection = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, ct);
            if (connection == null)
                return new TestConnectionResult(false, "Error", "Connection not found");

            // Get the PAT from Key Vault
            var patToken = await _keyVault.GetSecretAsync(connection.PatKeyVaultKey, ct);
            if (string.IsNullOrEmpty(patToken))
                return new TestConnectionResult(false, "Error", "PAT token not found in Key Vault");

            // Test connection using HTTP client
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($":{patToken}")));

            var url = $"https://dev.azure.com/{connection.Organization}/_apis/projects/{connection.Project}?api-version=7.0";
            var response = await httpClient.GetAsync(url, ct);

            if (response.IsSuccessStatusCode)
            {
                connection.RecordTestSuccess();
                await _repository.UpdateAsync(connection, ct);
                return new TestConnectionResult(true, "Connected");
            }

            var errorContent = await response.Content.ReadAsStringAsync(ct);
            connection.RecordTestFailure($"HTTP {(int)response.StatusCode}: {errorContent}");
            await _repository.UpdateAsync(connection, ct);
            return new TestConnectionResult(false, "Error", $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            var connection = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, ct);
            if (connection != null)
            {
                connection.RecordTestFailure(ex.Message);
                await _repository.UpdateAsync(connection, ct);
            }

            return new TestConnectionResult(false, "Error", ex.Message);
        }
    }
}

public class DeleteAdoConnectionHandler : IRequestHandler<DeleteAdoConnectionCommand, CommandResult>
{
    private readonly IAdoConnectionRepository _repository;
    private readonly IKeyVaultService _keyVault;
    private readonly ICurrentUser _currentUser;

    public DeleteAdoConnectionHandler(
        IAdoConnectionRepository repository,
        IKeyVaultService keyVault,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _keyVault = keyVault;
        _currentUser = currentUser;
    }

    public async Task<CommandResult> Handle(DeleteAdoConnectionCommand request, CancellationToken ct)
    {
        if (!_currentUser.IsAdmin)
            return new CommandResult(false, "Only admins can delete connections");

        try
        {
            var connection = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, ct);
            if (connection == null)
                return new CommandResult(false, "Connection not found");

            // Delete secret from Key Vault
            await _keyVault.DeleteSecretAsync(connection.PatKeyVaultKey, ct);

            // Delete the connection
            await _repository.DeleteAsync(_currentUser.TenantId, request.Id, ct);
            return new CommandResult(true);
        }
        catch (Exception ex)
        {
            return new CommandResult(false, ex.Message);
        }
    }
}
