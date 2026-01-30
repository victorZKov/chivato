using MediatR;

namespace Chivato.Application.Commands.Credentials;

public record TestCredentialCommand(string Type, string Id) : IRequest<TestCredentialResult>;

public record TestCredentialResult(
    bool Success,
    string? Error = null,
    DateTimeOffset? TestedAt = null
);

public record RotateCredentialCommand(
    string Type,
    string Id,
    string NewSecret,
    DateTimeOffset? ExpiresAt = null
) : IRequest<RotateCredentialResult>;

public record RotateCredentialResult(
    bool Success,
    string? Error = null,
    DateTimeOffset? RotatedAt = null
);
