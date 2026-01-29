using Chivato.Application.Commands.Pipelines;
using Chivato.Application.Common;
using Chivato.Domain.Entities;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class CreatePipelineHandler : IRequestHandler<CreatePipelineCommand, CreatePipelineResult>
{
    private readonly IPipelineRepository _repository;
    private readonly ICurrentUser _currentUser;

    public CreatePipelineHandler(IPipelineRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<CreatePipelineResult> Handle(CreatePipelineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var pipeline = Pipeline.Create(
                _currentUser.TenantId,
                request.Name,
                request.Organization,
                request.Project,
                request.RepositoryId,
                request.Branch,
                request.TerraformPath,
                request.SubscriptionId,
                request.ResourceGroup
            );

            await _repository.AddAsync(pipeline, cancellationToken);

            return new CreatePipelineResult(pipeline.Id, true);
        }
        catch (Exception ex)
        {
            return new CreatePipelineResult(string.Empty, false, ex.Message);
        }
    }
}
