namespace Chivato.Application.Common;

/// <summary>
/// Interface to access current user context
/// </summary>
public interface ICurrentUser
{
    string UserId { get; }
    string TenantId { get; }
    string Email { get; }
    string Name { get; }
    bool IsAdmin { get; }
    IReadOnlyList<string> Roles { get; }
}
