using Chivato.Functions.Models.Billing;

namespace Chivato.Functions.Services.Billing;

public interface IMollieService
{
    // Customer Management
    Task<string> CreateCustomerAsync(string email, string name, string? locale = null);
    Task<MollieCustomer?> GetCustomerAsync(string customerId);
    Task UpdateCustomerAsync(string customerId, string? email = null, string? name = null);

    // Payment Management
    Task<MolliePayment> CreatePaymentAsync(CreatePaymentRequest request);
    Task<MolliePayment?> GetPaymentAsync(string paymentId);

    // Subscription Management
    Task<MollieSubscription> CreateSubscriptionAsync(string customerId, CreateSubscriptionRequest request);
    Task<MollieSubscription?> GetSubscriptionAsync(string customerId, string subscriptionId);
    Task<MollieSubscription> UpdateSubscriptionAsync(string customerId, string subscriptionId, decimal? amount = null, string? description = null);
    Task CancelSubscriptionAsync(string customerId, string subscriptionId);

    // Payment Methods
    Task<IEnumerable<string>> GetAvailablePaymentMethodsAsync(decimal amount, string currency);
}

// DTOs for Mollie operations
public record CreatePaymentRequest(
    decimal Amount,
    string Currency,
    string Description,
    string RedirectUrl,
    string WebhookUrl,
    string? CustomerId = null,
    Dictionary<string, string>? Metadata = null
);

public record CreateSubscriptionRequest(
    decimal Amount,
    string Currency,
    string Interval, // "1 month", "12 months"
    string Description,
    string WebhookUrl,
    DateTimeOffset? StartDate = null,
    Dictionary<string, string>? Metadata = null
);

public record MollieCustomer(
    string Id,
    string Email,
    string? Name,
    string? Locale,
    DateTimeOffset CreatedAt
);

public record MolliePayment(
    string Id,
    string Status,
    decimal Amount,
    string Currency,
    string? Description,
    string? RedirectUrl,
    string? CheckoutUrl,
    string? Method,
    DateTimeOffset? PaidAt,
    DateTimeOffset? FailedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? ExpiredAt,
    Dictionary<string, string>? Metadata
);

public record MollieSubscription(
    string Id,
    string Status,
    decimal Amount,
    string Currency,
    string Interval,
    string? Description,
    DateTimeOffset? StartDate,
    DateTimeOffset? NextPaymentDate,
    DateTimeOffset? CancelledAt,
    Dictionary<string, string>? Metadata
);
