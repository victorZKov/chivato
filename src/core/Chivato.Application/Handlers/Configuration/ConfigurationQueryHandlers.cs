using Chivato.Application.Common;
using Chivato.Application.Queries.Configuration;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Configuration;

public class GetConfigurationHandler : IRequestHandler<GetConfigurationQuery, ConfigurationDto>
{
    private readonly IConfigurationRepository _configRepo;
    private readonly IAzureConnectionRepository _azureRepo;
    private readonly IAdoConnectionRepository _adoRepo;
    private readonly IEmailRecipientRepository _recipientRepo;
    private readonly ICurrentUser _currentUser;

    public GetConfigurationHandler(
        IConfigurationRepository configRepo,
        IAzureConnectionRepository azureRepo,
        IAdoConnectionRepository adoRepo,
        IEmailRecipientRepository recipientRepo,
        ICurrentUser currentUser)
    {
        _configRepo = configRepo;
        _azureRepo = azureRepo;
        _adoRepo = adoRepo;
        _recipientRepo = recipientRepo;
        _currentUser = currentUser;
    }

    public async Task<ConfigurationDto> Handle(GetConfigurationQuery request, CancellationToken ct)
    {
        var tenantId = _currentUser.TenantId;

        // Get settings
        var scanInterval = await _configRepo.GetScanIntervalHoursAsync(tenantId, ct);
        var emailEnabled = await _configRepo.GetEmailNotificationsEnabledAsync(tenantId, ct);
        var minSeverity = await _configRepo.GetValueAsync(tenantId, Domain.Entities.Configuration.Keys.MinimumSeverityForAlert, "High", ct);
        var maxScans = await _configRepo.GetValueAsync(tenantId, Domain.Entities.Configuration.Keys.MaxConcurrentScans, 2, ct);
        var retention = await _configRepo.GetValueAsync(tenantId, Domain.Entities.Configuration.Keys.RetentionDays, 90, ct);

        // Get connections
        var defaultAzure = await _azureRepo.GetDefaultAsync(tenantId, ct);
        var defaultAdo = await _adoRepo.GetDefaultAsync(tenantId, ct);
        var recipients = await _recipientRepo.GetActiveAsync(tenantId, ct);

        return new ConfigurationDto(
            scanInterval,
            emailEnabled,
            minSeverity,
            maxScans,
            retention,
            defaultAzure != null ? MapAzureConnection(defaultAzure) : null,
            defaultAdo != null ? MapAdoConnection(defaultAdo) : null,
            recipients.Select(MapEmailRecipient).ToList()
        );
    }

    private static AzureConnectionDto MapAzureConnection(AzureConnection c) => new(
        c.Id, c.Name, c.SubscriptionId, c.ClientId,
        c.Status.ToString(), c.LastTestedAt, c.LastTestError, c.IsDefault
    );

    private static AdoConnectionDto MapAdoConnection(AdoConnection c) => new(
        c.Id, c.Name, c.Organization, c.Project,
        c.Status.ToString(), c.LastTestedAt, c.LastTestError, c.IsDefault
    );

    private static EmailRecipientDto MapEmailRecipient(EmailRecipient r) => new(
        r.Id, r.Email, r.Name, r.IsActive,
        r.Preferences.MinimumSeverity.ToString(),
        r.Preferences.NotifyOnScanComplete,
        r.Preferences.NotifyOnNewDrift
    );
}

public class GetAzureConnectionsHandler : IRequestHandler<GetAzureConnectionsQuery, IReadOnlyList<AzureConnectionDto>>
{
    private readonly IAzureConnectionRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetAzureConnectionsHandler(IAzureConnectionRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AzureConnectionDto>> Handle(GetAzureConnectionsQuery request, CancellationToken ct)
    {
        var connections = await _repository.GetAllAsync(_currentUser.TenantId, ct);
        return connections.Select(c => new AzureConnectionDto(
            c.Id, c.Name, c.SubscriptionId, c.ClientId,
            c.Status.ToString(), c.LastTestedAt, c.LastTestError, c.IsDefault
        )).ToList();
    }
}

public class GetAdoConnectionsHandler : IRequestHandler<GetAdoConnectionsQuery, IReadOnlyList<AdoConnectionDto>>
{
    private readonly IAdoConnectionRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetAdoConnectionsHandler(IAdoConnectionRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AdoConnectionDto>> Handle(GetAdoConnectionsQuery request, CancellationToken ct)
    {
        var connections = await _repository.GetAllAsync(_currentUser.TenantId, ct);
        return connections.Select(c => new AdoConnectionDto(
            c.Id, c.Name, c.Organization, c.Project,
            c.Status.ToString(), c.LastTestedAt, c.LastTestError, c.IsDefault
        )).ToList();
    }
}

public class GetEmailRecipientsHandler : IRequestHandler<GetEmailRecipientsQuery, IReadOnlyList<EmailRecipientDto>>
{
    private readonly IEmailRecipientRepository _repository;
    private readonly ICurrentUser _currentUser;

    public GetEmailRecipientsHandler(IEmailRecipientRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<EmailRecipientDto>> Handle(GetEmailRecipientsQuery request, CancellationToken ct)
    {
        var recipients = await _repository.GetAllAsync(_currentUser.TenantId, ct);
        return recipients.Select(r => new EmailRecipientDto(
            r.Id, r.Email, r.Name, r.IsActive,
            r.Preferences.MinimumSeverity.ToString(),
            r.Preferences.NotifyOnScanComplete,
            r.Preferences.NotifyOnNewDrift
        )).ToList();
    }
}
