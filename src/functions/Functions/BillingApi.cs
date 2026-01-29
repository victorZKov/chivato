using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Chivato.Functions.Models.Billing;
using Chivato.Functions.Services.Billing;

namespace Chivato.Functions.Functions;

public class BillingApi
{
    private readonly ILogger<BillingApi> _logger;
    private readonly IBillingStorageService _billingStorage;
    private readonly ICheckoutService? _checkoutService;
    private readonly ISubscriptionService? _subscriptionService;

    public BillingApi(
        ILogger<BillingApi> logger,
        IBillingStorageService billingStorage,
        ICheckoutService? checkoutService = null,
        ISubscriptionService? subscriptionService = null)
    {
        _logger = logger;
        _billingStorage = billingStorage;
        _checkoutService = checkoutService;
        _subscriptionService = subscriptionService;
    }

    #region Plans

    [Function("GetPlans")]
    public async Task<HttpResponseData> GetPlans(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing/plans")] HttpRequestData req)
    {
        _logger.LogInformation("Getting available plans");

        var plans = await _billingStorage.GetActivePlansAsync();

        var result = plans.Select(p => new
        {
            id = p.RowKey,
            code = p.Code,
            name = p.Name,
            description = p.Description,
            priceMonthly = p.PriceMonthlyInCents / 100m,
            priceYearly = p.PriceYearlyInCents / 100m,
            currency = p.Currency,
            limits = new
            {
                maxPipelines = p.MaxPipelines,
                maxSubscriptions = p.MaxSubscriptions,
                maxResourceGroups = p.MaxResourceGroups,
                retentionDays = p.RetentionDays,
                aiAnalysisEnabled = p.AiAnalysisEnabled,
                emailReportsEnabled = p.EmailReportsEnabled
            }
        });

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("GetPlan")]
    public async Task<HttpResponseData> GetPlan(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing/plans/{planId}")] HttpRequestData req,
        string planId)
    {
        var plan = await _billingStorage.GetPlanAsync(planId);

        if (plan == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var result = new
        {
            id = plan.RowKey,
            code = plan.Code,
            name = plan.Name,
            description = plan.Description,
            priceMonthly = plan.PriceMonthlyInCents / 100m,
            priceYearly = plan.PriceYearlyInCents / 100m,
            currency = plan.Currency,
            limits = new
            {
                maxPipelines = plan.MaxPipelines,
                maxSubscriptions = plan.MaxSubscriptions,
                maxResourceGroups = plan.MaxResourceGroups,
                retentionDays = plan.RetentionDays,
                aiAnalysisEnabled = plan.AiAnalysisEnabled,
                emailReportsEnabled = plan.EmailReportsEnabled
            }
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    #endregion

    #region Subscriptions

    [Function("GetMySubscription")]
    public async Task<HttpResponseData> GetMySubscription(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing/subscription")] HttpRequestData req)
    {
        // TODO: Get tenant ID from auth token
        var tenantId = req.Headers.GetValues("X-Tenant-Id")?.FirstOrDefault() ?? "demo-tenant";

        var subscription = await _billingStorage.GetSubscriptionByTenantAsync(tenantId);

        if (subscription == null)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { hasSubscription = false });
            return response;
        }

        var plan = await _billingStorage.GetPlanAsync(subscription.PlanId);

        var result = new
        {
            hasSubscription = true,
            subscription = new
            {
                id = subscription.RowKey,
                status = subscription.Status,
                billingCycle = subscription.BillingCycle,
                currentPeriodStart = subscription.CurrentPeriodStart,
                currentPeriodEnd = subscription.CurrentPeriodEnd,
                trialEndsAt = subscription.TrialEndsAt,
                cancelledAt = subscription.CancelledAt,
                cancelAtPeriodEnd = subscription.CancelAtPeriodEnd
            },
            plan = plan == null ? null : new
            {
                id = plan.RowKey,
                code = plan.Code,
                name = plan.Name,
                priceMonthly = plan.PriceMonthlyInCents / 100m,
                priceYearly = plan.PriceYearlyInCents / 100m
            }
        };

        var responseOk = req.CreateResponse(HttpStatusCode.OK);
        await responseOk.WriteAsJsonAsync(result);
        return responseOk;
    }

    [Function("CreateCheckoutSession")]
    public async Task<HttpResponseData> CreateCheckoutSession(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "billing/checkout")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        var input = JsonSerializer.Deserialize<CheckoutInput>(body ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (input == null || string.IsNullOrEmpty(input.PlanId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "PlanId is required" });
            return badRequest;
        }

        // TODO: Get tenant ID from auth token
        var tenantId = req.Headers.GetValues("X-Tenant-Id")?.FirstOrDefault() ?? "demo-tenant";

        if (_checkoutService == null)
        {
            // Return mock checkout URL for development
            var mockResponse = req.CreateResponse(HttpStatusCode.OK);
            await mockResponse.WriteAsJsonAsync(new
            {
                sessionId = $"cs_{Guid.NewGuid():N}",
                checkoutUrl = $"https://checkout.mollie.com/mock/{Guid.NewGuid()}",
                status = "Pending"
            });
            return mockResponse;
        }

        var result = await _checkoutService.CreateCheckoutSessionAsync(new CreateCheckoutRequest(
            tenantId,
            input.PlanId,
            Enum.Parse<BillingCycle>(input.BillingCycle ?? "Monthly"),
            input.SuccessUrl ?? "https://chivato.azurewebsites.net/billing/success",
            input.CancelUrl ?? "https://chivato.azurewebsites.net/billing/cancel",
            input.TrialDays
        ));

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("CancelSubscription")]
    public async Task<HttpResponseData> CancelSubscription(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "billing/subscription/cancel")] HttpRequestData req)
    {
        // TODO: Get tenant ID from auth token
        var tenantId = req.Headers.GetValues("X-Tenant-Id")?.FirstOrDefault() ?? "demo-tenant";

        var subscription = await _billingStorage.GetSubscriptionByTenantAsync(tenantId);

        if (subscription == null)
        {
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteAsJsonAsync(new { error = "No active subscription found" });
            return notFound;
        }

        if (_subscriptionService != null)
        {
            await _subscriptionService.CancelSubscriptionAsync(subscription.RowKey, false);
        }
        else
        {
            // Mock cancellation for development
            subscription.CancelAtPeriodEnd = true;
            await _billingStorage.SaveSubscriptionAsync(subscription);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true, cancelAtPeriodEnd = true });
        return response;
    }

    #endregion

    #region Invoices & Payments

    [Function("GetInvoices")]
    public async Task<HttpResponseData> GetInvoices(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing/invoices")] HttpRequestData req)
    {
        // TODO: Get tenant ID from auth token
        var tenantId = req.Headers.GetValues("X-Tenant-Id")?.FirstOrDefault() ?? "demo-tenant";

        var invoices = await _billingStorage.GetInvoicesByTenantAsync(tenantId);

        var result = invoices.Select(i => new
        {
            id = i.RowKey,
            invoiceNumber = i.InvoiceNumber,
            status = i.Status,
            subtotal = i.SubtotalCents / 100m,
            tax = i.TaxCents / 100m,
            taxRate = i.TaxRate,
            total = i.TotalCents / 100m,
            currency = i.Currency,
            dueDate = i.DueDate,
            paidAt = i.PaidAt,
            createdAt = i.CreatedAt
        });

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    [Function("GetPayments")]
    public async Task<HttpResponseData> GetPayments(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing/payments")] HttpRequestData req)
    {
        // TODO: Get tenant ID from auth token
        var tenantId = req.Headers.GetValues("X-Tenant-Id")?.FirstOrDefault() ?? "demo-tenant";

        var payments = await _billingStorage.GetPaymentsByTenantAsync(tenantId);

        var result = payments.Select(p => new
        {
            id = p.RowKey,
            amount = p.AmountCents / 100m,
            currency = p.Currency,
            status = p.Status,
            paymentMethod = p.PaymentMethod,
            description = p.Description,
            paidAt = p.PaidAt,
            createdAt = p.CreatedAt
        });

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(result);
        return response;
    }

    #endregion

    #region Tenant Billing Info

    [Function("GetBillingInfo")]
    public async Task<HttpResponseData> GetBillingInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "billing/info")] HttpRequestData req)
    {
        // TODO: Get tenant ID from auth token
        var tenantId = req.Headers.GetValues("X-Tenant-Id")?.FirstOrDefault() ?? "demo-tenant";

        var billing = await _billingStorage.GetTenantBillingAsync(tenantId);

        if (billing == null)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { hasBillingInfo = false });
            return response;
        }

        var result = new
        {
            hasBillingInfo = true,
            email = billing.Email,
            companyName = billing.CompanyName,
            vatNumber = billing.VatNumber,
            country = billing.Country,
            addressLine1 = billing.AddressLine1,
            addressLine2 = billing.AddressLine2,
            city = billing.City,
            postalCode = billing.PostalCode
        };

        var responseOk = req.CreateResponse(HttpStatusCode.OK);
        await responseOk.WriteAsJsonAsync(result);
        return responseOk;
    }

    [Function("UpdateBillingInfo")]
    public async Task<HttpResponseData> UpdateBillingInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "billing/info")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        var input = JsonSerializer.Deserialize<BillingInfoInput>(body ?? "{}",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // TODO: Get tenant ID from auth token
        var tenantId = req.Headers.GetValues("X-Tenant-Id")?.FirstOrDefault() ?? "demo-tenant";

        var billing = await _billingStorage.GetTenantBillingAsync(tenantId) ?? new TenantBillingEntity
        {
            RowKey = tenantId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (input != null)
        {
            billing.Email = input.Email ?? billing.Email;
            billing.CompanyName = input.CompanyName ?? billing.CompanyName;
            billing.VatNumber = input.VatNumber ?? billing.VatNumber;
            billing.Country = input.Country ?? billing.Country;
            billing.AddressLine1 = input.AddressLine1 ?? billing.AddressLine1;
            billing.AddressLine2 = input.AddressLine2 ?? billing.AddressLine2;
            billing.City = input.City ?? billing.City;
            billing.PostalCode = input.PostalCode ?? billing.PostalCode;
        }

        await _billingStorage.SaveTenantBillingAsync(billing);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true });
        return response;
    }

    #endregion

    #region Webhooks

    [Function("MollieWebhook")]
    public async Task<HttpResponseData> MollieWebhook(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "webhooks/mollie")] HttpRequestData req)
    {
        var body = await req.ReadAsStringAsync();
        _logger.LogInformation("Received Mollie webhook: {Body}", body);

        // TODO: Implement webhook handler
        // Parse webhook payload and update payment/subscription status

        return req.CreateResponse(HttpStatusCode.OK);
    }

    #endregion
}

// Input DTOs
public record CheckoutInput(
    string PlanId,
    string? BillingCycle,
    string? SuccessUrl,
    string? CancelUrl,
    int TrialDays = 0);

public record BillingInfoInput(
    string? Email,
    string? CompanyName,
    string? VatNumber,
    string? Country,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? PostalCode);
