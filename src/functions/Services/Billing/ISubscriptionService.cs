using Chivato.Functions.Models.Billing;

namespace Chivato.Functions.Services.Billing;

public interface ISubscriptionService
{
    /// <summary>
    /// Creates a new subscription for a tenant
    /// </summary>
    Task<SubscriptionEntity> CreateSubscriptionAsync(
        string tenantId,
        string planId,
        BillingCycle billingCycle,
        int trialDays = 0);

    /// <summary>
    /// Gets the current subscription for a tenant
    /// </summary>
    Task<SubscriptionEntity?> GetCurrentSubscriptionAsync(string tenantId);

    /// <summary>
    /// Gets subscription by ID
    /// </summary>
    Task<SubscriptionEntity?> GetSubscriptionAsync(string subscriptionId);

    /// <summary>
    /// Upgrades to a higher plan (immediate change with prorating)
    /// </summary>
    Task<SubscriptionEntity> UpgradePlanAsync(string subscriptionId, string newPlanId);

    /// <summary>
    /// Downgrades to a lower plan (change at period end)
    /// </summary>
    Task<SubscriptionEntity> DowngradePlanAsync(string subscriptionId, string newPlanId);

    /// <summary>
    /// Cancels subscription (at period end or immediate)
    /// </summary>
    Task CancelSubscriptionAsync(string subscriptionId, bool immediate = false);

    /// <summary>
    /// Reactivates a cancelled subscription
    /// </summary>
    Task<SubscriptionEntity> ReactivateSubscriptionAsync(string subscriptionId);

    /// <summary>
    /// Changes billing cycle (monthly/yearly)
    /// </summary>
    Task<SubscriptionEntity> ChangeBillingCycleAsync(string subscriptionId, BillingCycle newCycle);

    /// <summary>
    /// Gets subscription with plan details
    /// </summary>
    Task<SubscriptionWithPlan?> GetSubscriptionWithPlanAsync(string tenantId);
}

public record SubscriptionWithPlan(
    SubscriptionEntity Subscription,
    PlanEntity Plan
);
