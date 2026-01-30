using MediatR;

namespace Chivato.Application.Queries.Credentials;

public record GetCredentialsQuery : IRequest<IEnumerable<CredentialStatusDto>>;

public record GetExpiringCredentialsQuery(int Days = 30) : IRequest<IEnumerable<CredentialStatusDto>>;

public record CredentialStatusDto(
    string Id,
    string Name,
    string Type,
    string Status,
    DateTimeOffset? ExpiresAt,
    int? DaysUntilExpiration,
    DateTimeOffset? LastTestedAt,
    string? LastTestResult
);
