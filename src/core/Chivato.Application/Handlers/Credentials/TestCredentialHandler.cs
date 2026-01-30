using Chivato.Application.Commands.Credentials;
using Chivato.Application.Common;
using Chivato.Domain.Interfaces;
using MediatR;

namespace Chivato.Application.Handlers.Credentials;

public class TestCredentialHandler : IRequestHandler<TestCredentialCommand, TestCredentialResult>
{
    private readonly ICredentialRepository _credentialRepository;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ICurrentUser _currentUser;

    public TestCredentialHandler(
        ICredentialRepository credentialRepository,
        IKeyVaultService keyVaultService,
        ICurrentUser currentUser)
    {
        _credentialRepository = credentialRepository;
        _keyVaultService = keyVaultService;
        _currentUser = currentUser;
    }

    public async Task<TestCredentialResult> Handle(TestCredentialCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var credential = await _credentialRepository.GetByIdAsync(
                _currentUser.TenantId,
                request.Id,
                cancellationToken
            );

            if (credential == null || !credential.TypeAsString.Equals(request.Type, StringComparison.OrdinalIgnoreCase))
            {
                return new TestCredentialResult(false, "Credential not found");
            }

            // Test by verifying the secret exists in Key Vault
            var secret = await _keyVaultService.GetSecretAsync(credential.KeyVaultSecretName, cancellationToken);

            var success = !string.IsNullOrEmpty(secret);
            var error = success ? null : "Secret not found in Key Vault";

            // Update credential with test result
            credential.UpdateTestResult(success, error);
            await _credentialRepository.UpdateAsync(credential, cancellationToken);

            return new TestCredentialResult(success, error, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new TestCredentialResult(false, ex.Message, DateTimeOffset.UtcNow);
        }
    }
}

public class RotateCredentialHandler : IRequestHandler<RotateCredentialCommand, RotateCredentialResult>
{
    private readonly ICredentialRepository _credentialRepository;
    private readonly IKeyVaultService _keyVaultService;
    private readonly ICurrentUser _currentUser;

    public RotateCredentialHandler(
        ICredentialRepository credentialRepository,
        IKeyVaultService keyVaultService,
        ICurrentUser currentUser)
    {
        _credentialRepository = credentialRepository;
        _keyVaultService = keyVaultService;
        _currentUser = currentUser;
    }

    public async Task<RotateCredentialResult> Handle(RotateCredentialCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var credential = await _credentialRepository.GetByIdAsync(
                _currentUser.TenantId,
                request.Id,
                cancellationToken
            );

            if (credential == null || !credential.TypeAsString.Equals(request.Type, StringComparison.OrdinalIgnoreCase))
            {
                return new RotateCredentialResult(false, "Credential not found");
            }

            // Update secret in Key Vault
            await _keyVaultService.SetSecretAsync(
                credential.KeyVaultSecretName,
                request.NewSecret,
                request.ExpiresAt,
                cancellationToken
            );

            // Update credential expiration if provided
            if (request.ExpiresAt.HasValue)
            {
                credential.UpdateExpiration(request.ExpiresAt.Value);
                await _credentialRepository.UpdateAsync(credential, cancellationToken);
            }

            return new RotateCredentialResult(true, null, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            return new RotateCredentialResult(false, ex.Message);
        }
    }
}
