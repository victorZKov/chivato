using Chivato.Application.Commands.Pipelines;
using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class DeletePipelineHandler : IRequestHandler<DeletePipelineCommand, DeletePipelineResult>
{
    private readonly IPipelineRepository _repository;
    private readonly ICurrentUser _currentUser;

    public DeletePipelineHandler(IPipelineRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<DeletePipelineResult> Handle(DeletePipelineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var pipeline = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

            if (pipeline == null)
                return new DeletePipelineResult(false, "Pipeline not found");

            await _repository.DeleteAsync(_currentUser.TenantId, request.Id, cancellationToken);

            return new DeletePipelineResult(true);
        }
        catch (Exception ex)
        {
            return new DeletePipelineResult(false, ex.Message);
        }
    }
}
