using Chivato.Application.Commands.Pipelines;
using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class UpdatePipelineHandler : IRequestHandler<UpdatePipelineCommand, UpdatePipelineResult>
{
    private readonly IPipelineRepository _repository;
    private readonly ICurrentUser _currentUser;

    public UpdatePipelineHandler(IPipelineRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<UpdatePipelineResult> Handle(UpdatePipelineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var pipeline = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

            if (pipeline == null)
                return new UpdatePipelineResult(false, "Pipeline not found");

            pipeline.Update(
                request.Name,
                request.Branch,
                request.TerraformPath,
                request.SubscriptionId,
                request.ResourceGroup
            );

            await _repository.UpdateAsync(pipeline, cancellationToken);

            return new UpdatePipelineResult(true);
        }
        catch (Exception ex)
        {
            return new UpdatePipelineResult(false, ex.Message);
        }
    }
}
