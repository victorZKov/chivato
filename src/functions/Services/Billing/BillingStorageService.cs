using Azure.Data.Tables;
using Chivato.Functions.Models.Billing;

namespace Chivato.Functions.Services.Billing;

public class BillingStorageService : IBillingStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private const string PlansTable = "Plans";
    private const string SubscriptionsTable = "Subscriptions";
    private const string PaymentsTable = "Payments";
    private const string InvoicesTable = "Invoices";
    private const string CheckoutSessionsTable = "CheckoutSessions";
    private const string TenantBillingTable = "TenantBilling";
    private const string CountersTable = "Counters";

    public BillingStorageService(string connectionString)
    {
        _tableServiceClient = new TableServiceClient(connectionString);
    }

    private TableClient GetTableClient(string tableName)
    {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        tableClient.CreateIfNotExists();
        return tableClient;
    }

    // Plans
    public async Task<IEnumerable<PlanEntity>> GetActivePlansAsync()
    {
        var tableClient = GetTableClient(PlansTable);
        var plans = new List<PlanEntity>();

        await foreach (var plan in tableClient.QueryAsync<PlanEntity>(p => p.PartitionKey == "plans" && p.IsActive))
        {
            plans.Add(plan);
        }

        return plans.OrderBy(p => p.SortOrder);
    }

    public async Task<PlanEntity?> GetPlanAsync(string planId)
    {
        var tableClient = GetTableClient(PlansTable);
        try
        {
            var response = await tableClient.GetEntityAsync<PlanEntity>("plans", planId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<PlanEntity?> GetPlanByCodeAsync(string code)
    {
        var tableClient = GetTableClient(PlansTable);

        await foreach (var plan in tableClient.QueryAsync<PlanEntity>(p => p.PartitionKey == "plans" && p.Code == code))
        {
            return plan;
        }

        return null;
    }

    public async Task SavePlanAsync(PlanEntity plan)
    {
        var tableClient = GetTableClient(PlansTable);
        plan.RowKey = string.IsNullOrEmpty(plan.RowKey) ? Guid.NewGuid().ToString() : plan.RowKey;
        await tableClient.UpsertEntityAsync(plan);
    }

    // Subscriptions
    public async Task<SubscriptionEntity?> GetSubscriptionAsync(string subscriptionId)
    {
        var tableClient = GetTableClient(SubscriptionsTable);
        try
        {
            var response = await tableClient.GetEntityAsync<SubscriptionEntity>("subscriptions", subscriptionId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<SubscriptionEntity?> GetSubscriptionByTenantAsync(string tenantId)
    {
        var tableClient = GetTableClient(SubscriptionsTable);

        await foreach (var sub in tableClient.QueryAsync<SubscriptionEntity>(
            s => s.PartitionKey == "subscriptions" && s.TenantId == tenantId))
        {
            // Return the first active or most recent subscription
            if (sub.Status == "Active" || sub.Status == "Trialing")
            {
                return sub;
            }
        }

        return null;
    }

    public async Task<SubscriptionEntity?> GetSubscriptionByMollieIdAsync(string mollieSubscriptionId)
    {
        var tableClient = GetTableClient(SubscriptionsTable);

        await foreach (var sub in tableClient.QueryAsync<SubscriptionEntity>(
            s => s.MollieSubscriptionId == mollieSubscriptionId))
        {
            return sub;
        }

        return null;
    }

    public async Task SaveSubscriptionAsync(SubscriptionEntity subscription)
    {
        var tableClient = GetTableClient(SubscriptionsTable);
        subscription.RowKey = string.IsNullOrEmpty(subscription.RowKey)
            ? Guid.NewGuid().ToString()
            : subscription.RowKey;
        await tableClient.UpsertEntityAsync(subscription);
    }

    // Payments
    public async Task<PaymentEntity?> GetPaymentAsync(string tenantId, string paymentId)
    {
        var tableClient = GetTableClient(PaymentsTable);
        try
        {
            var response = await tableClient.GetEntityAsync<PaymentEntity>(tenantId, paymentId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<PaymentEntity?> GetPaymentByMollieIdAsync(string molliePaymentId)
    {
        var tableClient = GetTableClient(PaymentsTable);

        await foreach (var payment in tableClient.QueryAsync<PaymentEntity>(p => p.MolliePaymentId == molliePaymentId))
        {
            return payment;
        }

        return null;
    }

    public async Task<IEnumerable<PaymentEntity>> GetPaymentsByTenantAsync(string tenantId)
    {
        var tableClient = GetTableClient(PaymentsTable);
        var payments = new List<PaymentEntity>();

        await foreach (var payment in tableClient.QueryAsync<PaymentEntity>(p => p.PartitionKey == tenantId))
        {
            payments.Add(payment);
        }

        return payments.OrderByDescending(p => p.CreatedAt);
    }

    public async Task SavePaymentAsync(PaymentEntity payment)
    {
        var tableClient = GetTableClient(PaymentsTable);
        payment.RowKey = string.IsNullOrEmpty(payment.RowKey) ? Guid.NewGuid().ToString() : payment.RowKey;
        await tableClient.UpsertEntityAsync(payment);
    }

    // Invoices
    public async Task<InvoiceEntity?> GetInvoiceAsync(string tenantId, string invoiceId)
    {
        var tableClient = GetTableClient(InvoicesTable);
        try
        {
            var response = await tableClient.GetEntityAsync<InvoiceEntity>(tenantId, invoiceId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<InvoiceEntity>> GetInvoicesByTenantAsync(string tenantId)
    {
        var tableClient = GetTableClient(InvoicesTable);
        var invoices = new List<InvoiceEntity>();

        await foreach (var invoice in tableClient.QueryAsync<InvoiceEntity>(i => i.PartitionKey == tenantId))
        {
            invoices.Add(invoice);
        }

        return invoices.OrderByDescending(i => i.CreatedAt);
    }

    public async Task<string> GenerateInvoiceNumberAsync()
    {
        var tableClient = GetTableClient(CountersTable);
        var year = DateTime.UtcNow.Year;
        var counterKey = $"invoice-{year}";

        try
        {
            var response = await tableClient.GetEntityAsync<CounterEntity>("counters", counterKey);
            var counter = response.Value;
            counter.Value++;
            await tableClient.UpdateEntityAsync(counter, counter.ETag);
            return $"CHIVATO-{year}-{counter.Value:D5}";
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            var counter = new CounterEntity { RowKey = counterKey, Value = 1 };
            await tableClient.AddEntityAsync(counter);
            return $"CHIVATO-{year}-00001";
        }
    }

    public async Task SaveInvoiceAsync(InvoiceEntity invoice)
    {
        var tableClient = GetTableClient(InvoicesTable);
        invoice.RowKey = string.IsNullOrEmpty(invoice.RowKey) ? Guid.NewGuid().ToString() : invoice.RowKey;
        await tableClient.UpsertEntityAsync(invoice);
    }

    // Checkout Sessions
    public async Task<CheckoutSessionEntity?> GetCheckoutSessionAsync(string sessionId)
    {
        var tableClient = GetTableClient(CheckoutSessionsTable);
        try
        {
            var response = await tableClient.GetEntityAsync<CheckoutSessionEntity>("checkout", sessionId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IEnumerable<CheckoutSessionEntity>> GetExpiredSessionsAsync()
    {
        var tableClient = GetTableClient(CheckoutSessionsTable);
        var sessions = new List<CheckoutSessionEntity>();
        var now = DateTimeOffset.UtcNow;

        await foreach (var session in tableClient.QueryAsync<CheckoutSessionEntity>(
            s => s.PartitionKey == "checkout" && s.Status == "Pending"))
        {
            if (session.ExpiresAt < now)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    public async Task SaveCheckoutSessionAsync(CheckoutSessionEntity session)
    {
        var tableClient = GetTableClient(CheckoutSessionsTable);
        await tableClient.UpsertEntityAsync(session);
    }

    public async Task DeleteCheckoutSessionAsync(string sessionId)
    {
        var tableClient = GetTableClient(CheckoutSessionsTable);
        await tableClient.DeleteEntityAsync("checkout", sessionId);
    }

    // Tenant Billing
    public async Task<TenantBillingEntity?> GetTenantBillingAsync(string tenantId)
    {
        var tableClient = GetTableClient(TenantBillingTable);
        try
        {
            var response = await tableClient.GetEntityAsync<TenantBillingEntity>("tenants", tenantId);
            return response.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveTenantBillingAsync(TenantBillingEntity tenant)
    {
        var tableClient = GetTableClient(TenantBillingTable);
        await tableClient.UpsertEntityAsync(tenant);
    }
}

// Helper entity for counters
public class CounterEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "counters";
    public string RowKey { get; set; } = string.Empty;
    public long Value { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }
}
