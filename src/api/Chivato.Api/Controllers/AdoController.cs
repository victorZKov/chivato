using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Chivato.Api.Controllers;

[ApiController]
[Route("api/ado")]
public class AdoController : ControllerBase
{
    private readonly IAdoService _adoService;
    private readonly IAdoConnectionRepository _connectionRepository;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<AdoController> _logger;

    public AdoController(
        IAdoService adoService,
        IAdoConnectionRepository connectionRepository,
        IKeyVaultService keyVaultService,
        ICurrentUser currentUser,
        ILogger<AdoController> logger)
    {
        _adoService = adoService;
        _connectionRepository = connectionRepository;
        _keyVaultService = keyVaultService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet("{connectionId}/projects")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjects(string connectionId)
    {
        var connection = await _connectionRepository.GetByIdAsync(_currentUser.TenantId, connectionId);
        if (connection == null)
        {
            _logger.LogWarning("ADO connection {ConnectionId} not found for tenant {TenantId}",
                connectionId, _currentUser.TenantId);
            return NotFound(new { error = "Connection not found" });
        }

        var patToken = await _keyVaultService.GetSecretAsync(connection.PatKeyVaultKey);
        if (string.IsNullOrEmpty(patToken))
        {
            _logger.LogWarning("PAT token not found for connection {ConnectionId}", connectionId);
            return BadRequest(new { error = "PAT token not found" });
        }

        var projects = await _adoService.GetProjectsAsync(connection.Organization, patToken);

        // Return just the project names as string array (matching UI expectation)
        return Ok(projects.Select(p => p.Name).ToArray());
    }

    [HttpGet("{connectionId}/projects/{project}/pipelines")]
    [ProducesResponseType(typeof(IEnumerable<PipelineInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPipelines(string connectionId, string project)
    {
        var connection = await _connectionRepository.GetByIdAsync(_currentUser.TenantId, connectionId);
        if (connection == null)
        {
            _logger.LogWarning("ADO connection {ConnectionId} not found for tenant {TenantId}",
                connectionId, _currentUser.TenantId);
            return NotFound(new { error = "Connection not found" });
        }

        var patToken = await _keyVaultService.GetSecretAsync(connection.PatKeyVaultKey);
        if (string.IsNullOrEmpty(patToken))
        {
            _logger.LogWarning("PAT token not found for connection {ConnectionId}", connectionId);
            return BadRequest(new { error = "PAT token not found" });
        }

        var pipelines = await _adoService.GetPipelinesAsync(connection.Organization, project, patToken);

        // Return as { id, name } objects (matching UI expectation)
        return Ok(pipelines.Select(p => new PipelineInfo(p.Id, p.Name)).ToArray());
    }
}

public record PipelineInfo(string Id, string Name);
