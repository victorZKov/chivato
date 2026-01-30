using Chivato.Application.Common;
using System.Security.Claims;

namespace Chivato.Api.Services;

/// <summary>
/// Implementation of ICurrentUser using HttpContext
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWebHostEnvironment _environment;

    // Default values for development
    private const string DevTenantId = "dev-tenant-00000000-0000-0000-0000-000000000000";
    private const string DevUserId = "dev-user-00000000-0000-0000-0000-000000000000";

    public CurrentUser(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
    private bool IsDevelopment => _environment.IsDevelopment();

    public string UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? User?.FindFirst("oid")?.Value
        ?? (IsDevelopment ? DevUserId : throw new InvalidOperationException("User ID not found"));

    public string TenantId => User?.FindFirst("tid")?.Value
        ?? User?.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid")?.Value
        ?? (IsDevelopment ? DevTenantId : throw new InvalidOperationException("Tenant ID not found"));

    public string Email => User?.FindFirst(ClaimTypes.Email)?.Value
        ?? User?.FindFirst("preferred_username")?.Value
        ?? (IsDevelopment ? "dev@chivato.local" : string.Empty);

    public string Name => User?.FindFirst(ClaimTypes.Name)?.Value
        ?? User?.FindFirst("name")?.Value
        ?? (IsDevelopment ? "Developer" : string.Empty);

    public bool IsAdmin => IsDevelopment || Roles.Contains("Admin");

    public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .ToList() ?? (IsDevelopment ? new List<string> { "Admin" } : new List<string>());
}
