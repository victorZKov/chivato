using Chivato.Functions.Models.Billing;

namespace Chivato.Functions.Services.Billing;

public interface IBillingStorageService
{
    // Plans
    Task<IEnumerable<PlanEntity>> GetActivePlansAsync();
    Task<PlanEntity?> GetPlanAsync(string planId);
    Task<PlanEntity?> GetPlanByCodeAsync(string code);
    Task SavePlanAsync(PlanEntity plan);

    // Subscriptions
    Task<SubscriptionEntity?> GetSubscriptionAsync(string subscriptionId);
    Task<SubscriptionEntity?> GetSubscriptionByTenantAsync(string tenantId);
    Task<SubscriptionEntity?> GetSubscriptionByMollieIdAsync(string mollieSubscriptionId);
    Task SaveSubscriptionAsync(SubscriptionEntity subscription);

    // Payments
    Task<PaymentEntity?> GetPaymentAsync(string tenantId, string paymentId);
    Task<PaymentEntity?> GetPaymentByMollieIdAsync(string molliePaymentId);
    Task<IEnumerable<PaymentEntity>> GetPaymentsByTenantAsync(string tenantId);
    Task SavePaymentAsync(PaymentEntity payment);

    // Invoices
    Task<InvoiceEntity?> GetInvoiceAsync(string tenantId, string invoiceId);
    Task<IEnumerable<InvoiceEntity>> GetInvoicesByTenantAsync(string tenantId);
    Task<string> GenerateInvoiceNumberAsync();
    Task SaveInvoiceAsync(InvoiceEntity invoice);

    // Checkout Sessions
    Task<CheckoutSessionEntity?> GetCheckoutSessionAsync(string sessionId);
    Task<IEnumerable<CheckoutSessionEntity>> GetExpiredSessionsAsync();
    Task SaveCheckoutSessionAsync(CheckoutSessionEntity session);
    Task DeleteCheckoutSessionAsync(string sessionId);

    // Tenant Billing
    Task<TenantBillingEntity?> GetTenantBillingAsync(string tenantId);
    Task SaveTenantBillingAsync(TenantBillingEntity tenant);
}
