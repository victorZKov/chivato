using Chivato.Application.Commands.Pipelines;
using Chivato.Application.Common;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class CreatePipelinesFromConnectionsHandler
    : IRequestHandler<CreatePipelinesFromConnectionsCommand, CreatePipelinesResult>
{
    private readonly IPipelineRepository _pipelineRepository;
    private readonly IAdoConnectionRepository _adoConnectionRepository;
    private readonly IAzureConnectionRepository _azureConnectionRepository;
    private readonly IAdoService _adoService;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ICurrentUser _currentUser;

    public CreatePipelinesFromConnectionsHandler(
        IPipelineRepository pipelineRepository,
        IAdoConnectionRepository adoConnectionRepository,
        IAzureConnectionRepository azureConnectionRepository,
        IAdoService adoService,
        IKeyVaultService keyVaultService,
        ICurrentUser currentUser)
    {
        _pipelineRepository = pipelineRepository;
        _adoConnectionRepository = adoConnectionRepository;
        _azureConnectionRepository = azureConnectionRepository;
        _adoService = adoService;
        _keyVaultService = keyVaultService;
        _currentUser = currentUser;
    }

    public async Task<CreatePipelinesResult> Handle(
        CreatePipelinesFromConnectionsCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate ADO connection exists
            var adoConnection = await _adoConnectionRepository.GetByIdAsync(
                _currentUser.TenantId, request.AdoConnectionId, cancellationToken);

            if (adoConnection == null)
            {
                return new CreatePipelinesResult(
                    Array.Empty<string>(), false, "ADO connection not found");
            }

            // Validate Azure connection exists
            var azureConnection = await _azureConnectionRepository.GetByIdAsync(
                _currentUser.TenantId, request.AzureConnectionId, cancellationToken);

            if (azureConnection == null)
            {
                return new CreatePipelinesResult(
                    Array.Empty<string>(), false, "Azure connection not found");
            }

            // Get PAT token to fetch pipeline details
            var patToken = await _keyVaultService.GetSecretAsync(adoConnection.PatKeyVaultKey, cancellationToken);
            if (string.IsNullOrEmpty(patToken))
            {
                return new CreatePipelinesResult(
                    Array.Empty<string>(), false, "Could not retrieve ADO PAT token");
            }

            // Fetch all pipelines from ADO to get their names
            var adoPipelines = await _adoService.GetPipelinesAsync(
                adoConnection.Organization, request.ProjectName, patToken, cancellationToken);

            var pipelineDict = adoPipelines.ToDictionary(p => p.Id, p => p.Name);

            var createdIds = new List<string>();
            var errors = new List<string>();

            foreach (var pipelineId in request.PipelineIds)
            {
                // Get pipeline name from ADO
                if (!pipelineDict.TryGetValue(pipelineId, out var pipelineName))
                {
                    errors.Add($"Pipeline {pipelineId} not found in ADO");
                    continue;
                }

                // Create the pipeline entity
                var pipeline = Pipeline.CreateFromConnections(
                    tenantId: _currentUser.TenantId,
                    adoConnectionId: request.AdoConnectionId,
                    azureConnectionId: request.AzureConnectionId,
                    organization: adoConnection.Organization,
                    project: request.ProjectName,
                    pipelineId: pipelineId,
                    pipelineName: pipelineName,
                    subscriptionId: azureConnection.SubscriptionId
                );

                await _pipelineRepository.AddAsync(pipeline, cancellationToken);
                createdIds.Add(pipeline.Id);
            }

            if (createdIds.Count == 0 && errors.Count > 0)
            {
                return new CreatePipelinesResult(
                    Array.Empty<string>(), false, string.Join("; ", errors));
            }

            return new CreatePipelinesResult(createdIds, true);
        }
        catch (Exception ex)
        {
            return new CreatePipelinesResult(Array.Empty<string>(), false, ex.Message);
        }
    }
}
