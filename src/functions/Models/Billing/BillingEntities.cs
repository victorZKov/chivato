using Azure;
using Azure.Data.Tables;

namespace Chivato.Functions.Models.Billing;

/// <summary>
/// Subscription plan definition
/// </summary>
public class PlanEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "plans";
    public string RowKey { get; set; } = string.Empty; // Plan ID (guid)
    public string Code { get; set; } = string.Empty; // starter, pro, enterprise
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long PriceMonthlyInCents { get; set; }
    public long PriceYearlyInCents { get; set; }
    public string Currency { get; set; } = "EUR";

    // Limits as JSON
    public int MaxPipelines { get; set; }
    public int MaxSubscriptions { get; set; }
    public int MaxResourceGroups { get; set; }
    public int RetentionDays { get; set; }
    public bool AiAnalysisEnabled { get; set; }
    public bool EmailReportsEnabled { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Tenant subscription
/// </summary>
public class SubscriptionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "subscriptions";
    public string RowKey { get; set; } = string.Empty; // Subscription ID (guid)
    public string TenantId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string Status { get; set; } = "Active"; // SubscriptionStatus
    public string BillingCycle { get; set; } = "Monthly"; // BillingCycle
    public DateTimeOffset CurrentPeriodStart { get; set; }
    public DateTimeOffset CurrentPeriodEnd { get; set; }
    public DateTimeOffset? TrialEndsAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public string? MollieSubscriptionId { get; set; }
    public string? MollieCustomerId { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Payment record
/// </summary>
public class PaymentEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // TenantId
    public string RowKey { get; set; } = string.Empty; // Payment ID (guid)
    public string? InvoiceId { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = "Pending"; // PaymentStatus
    public string? PaymentMethod { get; set; } // ideal, creditcard, etc
    public string? MolliePaymentId { get; set; }
    public string? MolliePaymentUrl { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
    public long? RefundAmountCents { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Invoice record
/// </summary>
public class InvoiceEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // TenantId
    public string RowKey { get; set; } = string.Empty; // Invoice ID (guid)
    public string InvoiceNumber { get; set; } = string.Empty; // CHIVATO-2026-00001
    public string Status { get; set; } = "Draft"; // InvoiceStatus
    public string Currency { get; set; } = "EUR";
    public long SubtotalCents { get; set; }
    public long TaxCents { get; set; }
    public decimal TaxRate { get; set; }
    public long TotalCents { get; set; }
    public string? Description { get; set; }
    public string? SubscriptionId { get; set; }
    public string? MolliePaymentId { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Checkout session for payment flow
/// </summary>
public class CheckoutSessionEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "checkout";
    public string RowKey { get; set; } = string.Empty; // Session ID (cs_xxxx)
    public string TenantId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = "Monthly";
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = "Pending"; // CheckoutSessionStatus
    public string? MolliePaymentId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? SubscriptionId { get; set; }
    public string SuccessUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public int TrialDays { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

/// <summary>
/// Tenant billing information
/// </summary>
public class TenantBillingEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "tenants";
    public string RowKey { get; set; } = string.Empty; // Tenant ID (from Entra ID object id)
    public string? MollieCustomerId { get; set; }
    public string? Email { get; set; }
    public string? CompanyName { get; set; }
    public string? VatNumber { get; set; }
    public string? Country { get; set; } // ISO 3166-1 alpha-2 (ES, NL, DE)
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? PostalCode { get; set; }
    public string? CurrentSubscriptionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
