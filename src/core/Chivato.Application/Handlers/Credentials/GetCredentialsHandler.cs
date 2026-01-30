using Chivato.Application.Common;
using Chivato.Application.Queries.Credentials;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Credentials;

public class GetCredentialsHandler : IRequestHandler<GetCredentialsQuery, IEnumerable<CredentialStatusDto>>
{
    private readonly ICredentialRepository _credentialRepository;
    private readonly ICurrentUser _currentUser;

    public GetCredentialsHandler(ICredentialRepository credentialRepository, ICurrentUser currentUser)
    {
        _credentialRepository = credentialRepository;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<CredentialStatusDto>> Handle(GetCredentialsQuery request, CancellationToken cancellationToken)
    {
        var credentials = await _credentialRepository.GetAllAsync(_currentUser.TenantId, cancellationToken);

        return credentials.Select(c => new CredentialStatusDto(
            Id: c.Id,
            Name: c.Name,
            Type: c.TypeAsString,
            Status: GetStatusFromExpiration(c.ExpiresAt),
            ExpiresAt: c.ExpiresAt,
            DaysUntilExpiration: c.ExpiresAt.HasValue
                ? (int)(c.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays
                : null,
            LastTestedAt: c.LastTestedAt,
            LastTestResult: c.LastTestResult
        ));
    }

    private static string GetStatusFromExpiration(DateTimeOffset? expiresAt)
    {
        if (!expiresAt.HasValue) return "ok";

        var daysUntil = (expiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

        if (daysUntil < 0) return "expired";
        if (daysUntil <= 7) return "danger";
        if (daysUntil <= 30) return "warning";
        return "ok";
    }
}

public class GetExpiringCredentialsHandler : IRequestHandler<GetExpiringCredentialsQuery, IEnumerable<CredentialStatusDto>>
{
    private readonly ICredentialRepository _credentialRepository;
    private readonly ICurrentUser _currentUser;

    public GetExpiringCredentialsHandler(ICredentialRepository credentialRepository, ICurrentUser currentUser)
    {
        _credentialRepository = credentialRepository;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<CredentialStatusDto>> Handle(GetExpiringCredentialsQuery request, CancellationToken cancellationToken)
    {
        var credentials = await _credentialRepository.GetExpiringAsync(_currentUser.TenantId, request.Days, cancellationToken);

        return credentials
            .Select(c => new CredentialStatusDto(
                Id: c.Id,
                Name: c.Name,
                Type: c.TypeAsString,
                Status: GetStatusFromExpiration(c.ExpiresAt),
                ExpiresAt: c.ExpiresAt,
                DaysUntilExpiration: c.ExpiresAt.HasValue
                    ? (int)(c.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays
                    : null,
                LastTestedAt: c.LastTestedAt,
                LastTestResult: c.LastTestResult
            ))
            .OrderBy(c => c.DaysUntilExpiration);
    }

    private static string GetStatusFromExpiration(DateTimeOffset? expiresAt)
    {
        if (!expiresAt.HasValue) return "ok";

        var daysUntil = (expiresAt.Value - DateTimeOffset.UtcNow).TotalDays;

        if (daysUntil < 0) return "expired";
        if (daysUntil <= 7) return "danger";
        if (daysUntil <= 30) return "warning";
        return "ok";
    }
}
