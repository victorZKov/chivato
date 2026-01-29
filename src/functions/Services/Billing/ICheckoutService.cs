using Chivato.Functions.Models.Billing;

namespace Chivato.Functions.Services.Billing;

public interface ICheckoutService
{
    /// <summary>
    /// Creates a new checkout session for subscription
    /// </summary>
    Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutRequest request);

    /// <summary>
    /// Handles successful payment callback
    /// </summary>
    Task HandleSuccessAsync(string sessionId);

    /// <summary>
    /// Handles cancelled payment callback
    /// </summary>
    Task HandleCancelAsync(string sessionId);

    /// <summary>
    /// Gets checkout session by ID
    /// </summary>
    Task<CheckoutSessionEntity?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Expires abandoned sessions older than 24 hours
    /// </summary>
    Task ExpireAbandonedSessionsAsync();
}

public record CreateCheckoutRequest(
    string TenantId,
    string PlanId,
    BillingCycle BillingCycle,
    string SuccessUrl,
    string CancelUrl,
    int TrialDays = 0
);

public record CheckoutSessionResult(
    string SessionId,
    string? CheckoutUrl,
    string Status
);
