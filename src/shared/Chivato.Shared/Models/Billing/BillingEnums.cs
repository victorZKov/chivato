namespace Chivato.Shared.Models.Billing;

public enum PaymentStatus
{
    Pending,
    Authorized,
    Paid,
    Failed,
    Cancelled,
    Refunded,
    ChargedBack,
    Expired
}

public enum SubscriptionStatus
{
    Trialing,
    Active,
    PastDue,
    Cancelled,
    Expired,
    Paused
}

public enum InvoiceStatus
{
    Draft,
    Open,
    Paid,
    Void,
    Uncollectible
}

public enum BillingCycle
{
    Monthly,
    Yearly
}

public enum CheckoutSessionStatus
{
    Pending,
    Completed,
    Cancelled,
    Failed,
    Expired
}
