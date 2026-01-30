using Chivato.Application.DTOs;
using MediatR;

namespace Chivato.Application.Queries.Pipelines;

public record GetPipelinesQuery() : IRequest<IReadOnlyList<PipelineDto>>;

public record GetPipelineByIdQuery(string Id) : IRequest<PipelineDetailDto?>;
