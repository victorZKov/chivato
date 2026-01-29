using Chivato.Application.Common;
using System.Security.Claims;

namespace Chivato.Api.Services;

/// <summary>
/// Implementation of ICurrentUser using HttpContext
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User?.FindFirst("oid")?.Value
        ?? throw new InvalidOperationException("User ID not found");

    public string TenantId => User?.FindFirst("tid")?.Value
        ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
        ?? throw new InvalidOperationException("Tenant ID not found");

    public string Email => User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("preferred_username")?.Value
        ?? string.Empty;

    public string Name => User?.FindFirst(ClaimTypes.Name)?.Value
        ?? User?.FindFirst("name")?.Value
        ?? string.Empty;

    public bool IsAdmin => Roles.Contains("Admin");

    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? new List<string>();
}
