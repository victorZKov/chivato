using Chivato.Application.Commands.Pipelines;
using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Pipelines;

public class ActivatePipelineHandler : IRequestHandler<ActivatePipelineCommand, TogglePipelineResult>
{
    private readonly IPipelineRepository _repository;
    private readonly ICurrentUser _currentUser;

    public ActivatePipelineHandler(IPipelineRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<TogglePipelineResult> Handle(ActivatePipelineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var pipeline = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

            if (pipeline == null)
                return new TogglePipelineResult(false, string.Empty, "Pipeline not found");

            pipeline.Activate();
            await _repository.UpdateAsync(pipeline, cancellationToken);

            return new TogglePipelineResult(true, pipeline.Status.ToString());
        }
        catch (Exception ex)
        {
            return new TogglePipelineResult(false, string.Empty, ex.Message);
        }
    }
}

public class DeactivatePipelineHandler : IRequestHandler<DeactivatePipelineCommand, TogglePipelineResult>
{
    private readonly IPipelineRepository _repository;
    private readonly ICurrentUser _currentUser;

    public DeactivatePipelineHandler(IPipelineRepository repository, ICurrentUser currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<TogglePipelineResult> Handle(DeactivatePipelineCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var pipeline = await _repository.GetByIdAsync(_currentUser.TenantId, request.Id, cancellationToken);

            if (pipeline == null)
                return new TogglePipelineResult(false, string.Empty, "Pipeline not found");

            pipeline.Deactivate();
            await _repository.UpdateAsync(pipeline, cancellationToken);

            return new TogglePipelineResult(true, pipeline.Status.ToString());
        }
        catch (Exception ex)
        {
            return new TogglePipelineResult(false, string.Empty, ex.Message);
        }
    }
}
