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
                    tenantId, request.Name, request.SubscriptionId,
                    request.ClientId, secretName, request.IsDefault
                );
                await _repository.AddAsync(connection, ct);
            }
            else
            {
                // Update existing
                connection = await _repository.GetByIdAsync(tenantId, request.Id, ct)
                    ?? throw new InvalidOperationException("Connection not found");

                connection.Update(request.Name, request.SubscriptionId, request.ClientId, secretName);

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
    private readonly IAzureResourceService _azureService;
    private readonly ICurrentUser _currentUser;

    public TestAzureConnectionHandler(
        IAzureConnectionRepository repository,
        IKeyVaultService keyVault,
        IAzureResourceService azureService,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _keyVault = keyVault;
        _azureService = azureService;
        _currentUser = currentUser;
    }

    public async Task<TestConnectionResult> Handle(TestAzureConnectionCommand request, CancellationToken ct)
    {
        try
        {
            var connection = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, ct);
            if (connection == null)
                return new TestConnectionResult(false, "Error", "Connection not found");

            // Try to list resources to test connection
            var resources = await _azureService.GetResourcesInGroupAsync(
                connection.SubscriptionId, "test-rg", ct);

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
