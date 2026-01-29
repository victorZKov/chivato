using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Mollie.Api.Client;
using Mollie.Api.Client.Abstract;
using Mollie.Api.Models;
using Mollie.Api.Models.Customer.Request;
using Mollie.Api.Models.Customer.Response;
using Mollie.Api.Models.Payment.Request;
using Mollie.Api.Models.Payment.Response;
using Mollie.Api.Models.PaymentMethod.Response;
using Mollie.Api.Models.Subscription.Request;
using Mollie.Api.Models.Subscription.Response;

namespace Chivato.Functions.Services.Billing;

public class MollieOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string WebhookBaseUrl { get; set; } = string.Empty;
    public string DefaultLocale { get; set; } = "es_ES";
    public bool TestMode { get; set; } = true;
}

public class MollieService : IMollieService
{
    private readonly ILogger<MollieService> _logger;
    private readonly MollieOptions _options;
    private readonly CustomerClient _customerClient;
    private readonly PaymentClient _paymentClient;
    private readonly SubscriptionClient _subscriptionClient;
    private readonly PaymentMethodClient _paymentMethodClient;

    public MollieService(
        ILogger<MollieService> logger,
        IOptions<MollieOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Initialize Mollie clients with API key
        _customerClient = new CustomerClient(_options.ApiKey);
        _paymentClient = new PaymentClient(_options.ApiKey);
        _subscriptionClient = new SubscriptionClient(_options.ApiKey);
        _paymentMethodClient = new PaymentMethodClient(_options.ApiKey);
    }

    // Customer Management
    public async Task<string> CreateCustomerAsync(string email, string name, string? locale = null)
    {
        _logger.LogInformation("Creating Mollie customer for email: {Email}", email);

        var request = new CustomerRequest
        {
            Email = email,
            Name = name,
            Locale = locale ?? _options.DefaultLocale
        };

        var response = await _customerClient.CreateCustomerAsync(request);
        _logger.LogInformation("Created Mollie customer: {CustomerId}", response.Id);

        return response.Id;
    }

    public async Task<MollieCustomer?> GetCustomerAsync(string customerId)
    {
        try
        {
            var response = await _customerClient.GetCustomerAsync(customerId);
            return new MollieCustomer(
                response.Id,
                response.Email ?? string.Empty,
                response.Name ?? string.Empty,
                response.Locale ?? _options.DefaultLocale,
                new DateTimeOffset(response.CreatedAt, TimeSpan.Zero)
            );
        }
        catch (MollieApiException ex) when (ex.Details?.Status == 404)
        {
            return null;
        }
    }

    public async Task UpdateCustomerAsync(string customerId, string? email = null, string? name = null)
    {
        var request = new CustomerRequest();
        if (!string.IsNullOrEmpty(email)) request.Email = email;
        if (!string.IsNullOrEmpty(name)) request.Name = name;

        await _customerClient.UpdateCustomerAsync(customerId, request);
        _logger.LogInformation("Updated Mollie customer: {CustomerId}", customerId);
    }

    // Payment Management
    public async Task<MolliePayment> CreatePaymentAsync(CreatePaymentRequest request)
    {
        _logger.LogInformation("Creating Mollie payment: {Amount} {Currency}", request.Amount, request.Currency);

        var paymentRequest = new PaymentRequest
        {
            Amount = new Amount(request.Currency, request.Amount),
            Description = request.Description,
            RedirectUrl = request.RedirectUrl,
            WebhookUrl = request.WebhookUrl,
            CustomerId = request.CustomerId,
            Metadata = request.Metadata != null ? SerializeMetadata(request.Metadata) : null
        };

        var response = await _paymentClient.CreatePaymentAsync(paymentRequest);
        _logger.LogInformation("Created Mollie payment: {PaymentId}", response.Id);

        return MapPayment(response);
    }

    public async Task<MolliePayment?> GetPaymentAsync(string paymentId)
    {
        try
        {
            var response = await _paymentClient.GetPaymentAsync(paymentId);
            return MapPayment(response);
        }
        catch (MollieApiException ex) when (ex.Details?.Status == 404)
        {
            return null;
        }
    }

    // Subscription Management
    public async Task<MollieSubscription> CreateSubscriptionAsync(string customerId, CreateSubscriptionRequest request)
    {
        _logger.LogInformation("Creating Mollie subscription for customer: {CustomerId}", customerId);

        var subscriptionRequest = new SubscriptionRequest
        {
            Amount = new Amount(request.Currency, request.Amount),
            Interval = request.Interval,
            Description = request.Description,
            WebhookUrl = request.WebhookUrl,
            StartDate = request.StartDate?.DateTime,
            Metadata = request.Metadata != null ? SerializeMetadata(request.Metadata) : null
        };

        var response = await _subscriptionClient.CreateSubscriptionAsync(customerId, subscriptionRequest);
        _logger.LogInformation("Created Mollie subscription: {SubscriptionId}", response.Id);

        return MapSubscription(response);
    }

    public async Task<MollieSubscription?> GetSubscriptionAsync(string customerId, string subscriptionId)
    {
        try
        {
            var response = await _subscriptionClient.GetSubscriptionAsync(customerId, subscriptionId);
            return MapSubscription(response);
        }
        catch (MollieApiException ex) when (ex.Details?.Status == 404)
        {
            return null;
        }
    }

    public async Task<MollieSubscription> UpdateSubscriptionAsync(
        string customerId,
        string subscriptionId,
        decimal? amount = null,
        string? description = null)
    {
        var request = new SubscriptionUpdateRequest();
        if (amount.HasValue)
        {
            request.Amount = new Amount("EUR", amount.Value);
        }
        if (!string.IsNullOrEmpty(description))
        {
            request.Description = description;
        }

        var response = await _subscriptionClient.UpdateSubscriptionAsync(customerId, subscriptionId, request);
        _logger.LogInformation("Updated Mollie subscription: {SubscriptionId}", subscriptionId);

        return MapSubscription(response);
    }

    public async Task CancelSubscriptionAsync(string customerId, string subscriptionId)
    {
        await _subscriptionClient.CancelSubscriptionAsync(customerId, subscriptionId);
        _logger.LogInformation("Cancelled Mollie subscription: {SubscriptionId}", subscriptionId);
    }

    // Payment Methods
    public async Task<IEnumerable<string>> GetAvailablePaymentMethodsAsync(decimal amount, string currency)
    {
        var response = await _paymentMethodClient.GetPaymentMethodListAsync(
            amount: new Amount(currency, amount)
        );

        return response.Items?.Select(m => m.Id) ?? Enumerable.Empty<string>();
    }

    // Helper methods
    private static string? SerializeMetadata(Dictionary<string, string>? metadata)
    {
        if (metadata == null || metadata.Count == 0) return null;
        return System.Text.Json.JsonSerializer.Serialize(metadata);
    }

    private static Dictionary<string, string>? DeserializeMetadata(string? metadata)
    {
        if (string.IsNullOrEmpty(metadata)) return null;
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(metadata);
        }
        catch
        {
            return null;
        }
    }

    private static MolliePayment MapPayment(PaymentResponse response)
    {
        return new MolliePayment(
            response.Id,
            response.Status,
            ParseAmount(response.Amount?.Value),
            response.Amount?.Currency ?? "EUR",
            response.Description ?? string.Empty,
            response.RedirectUrl ?? string.Empty,
            response.Links?.Checkout?.Href,
            response.Method,
            response.PaidAt,
            response.FailedAt,
            response.CanceledAt,
            response.ExpiredAt,
            DeserializeMetadata(response.Metadata)
        );
    }

    private static MollieSubscription MapSubscription(SubscriptionResponse response)
    {
        return new MollieSubscription(
            response.Id,
            response.Status,
            ParseAmount(response.Amount?.Value),
            response.Amount?.Currency ?? "EUR",
            response.Interval ?? string.Empty,
            response.Description ?? string.Empty,
            response.StartDate.HasValue ? new DateTimeOffset(response.StartDate.Value, TimeSpan.Zero) : null,
            response.NextPaymentDate.HasValue ? new DateTimeOffset(response.NextPaymentDate.Value, TimeSpan.Zero) : null,
            response.CanceledAt,
            DeserializeMetadata(response.Metadata)
        );
    }

    private static decimal ParseAmount(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        return decimal.TryParse(value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;
    }
}
